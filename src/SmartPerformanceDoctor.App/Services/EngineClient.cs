using System.Diagnostics;
using System.Text.Json;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Services;

public sealed class EngineClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly object ActiveProcessLock = new();
    private static Process? _activeProcess;

    public async Task<EngineResponse> SendAsync(
        EngineEnvelope request,
        CancellationToken cancellationToken,
        Action<EngineEvent>? onEvent = null)
    {
        var corePath = ResolveCorePath();
        var module = request.Params.TryGetValue("module", out var moduleValue) ? moduleValue : "system";

        if (!File.Exists(corePath))
        {
            return DemoResponse(request, "core-not-found", $"Core 실행 파일을 찾지 못했습니다: {corePath}", onEvent);
        }

        var baseDir = AppContext.BaseDirectory;
        var startInfo = new ProcessStartInfo
        {
            FileName = corePath,
            WorkingDirectory = baseDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return DemoResponse(request, "failed", "Core 프로세스 시작에 실패했습니다.", onEvent);
        }

        try
        {
        RegisterActiveProcess(process);

        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ResolveModuleTimeout(module));
        using var killOnCancel = timeoutCts.Token.Register(KillActiveProcess);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!process.HasExited && !timeoutCts.Token.IsCancellationRequested)
                {
                    await process.StandardError.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                }
            }
            catch
            {
                // stderr drain prevents pipe back-pressure; ignore read failures.
            }
        }, timeoutCts.Token);

        var events = new List<EngineEvent>();
        EngineResponse? finalResponse = null;

        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.TrimStart().StartsWith('{'))
            {
                continue;
            }

            EngineFrame? frame;
            try
            {
                frame = JsonSerializer.Deserialize<EngineFrame>(line, JsonOptions);
            }
            catch
            {
                continue;
            }

            if (frame?.FrameType == "event" && frame.Event is not null)
            {
                events.Add(frame.Event);
                var localized = frame.Event with
                {
                    Message = DiagnosticMessageLocalizer.Localize(frame.Event.Message)
                };
                onEvent?.Invoke(localized);
                continue;
            }

            if (frame?.FrameType == "response" && frame.Response is not null)
            {
                finalResponse = frame.Response;
                break;
            }
        }

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignored
                }
            }
        }

        if (finalResponse is not null)
        {
            var merged = finalResponse with { Events = events.Concat(finalResponse.Events).ToArray() };
            RecordKnowledge(request, merged);
            return merged;
        }

        if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new EngineResponse
            {
                RequestId = request.Id,
                Status = "timeout",
                Message = $"{module} 모듈 점검이 시간 제한을 초과했습니다. 다음 단계로 계속합니다.",
                Events = events,
                Intelligence = BuildTimeoutIntelligence(module)
            };
        }

        var stderr = await process.StandardError.ReadToEndAsync(CancellationToken.None);
        return new EngineResponse
        {
            RequestId = request.Id,
            Status = "no-final-response",
            Message = string.IsNullOrWhiteSpace(stderr)
                ? "Core 최종 응답을 받지 못했습니다. 다음 단계로 계속합니다."
                : $"Core 최종 응답 없음 / STDERR: {stderr}",
            Events = events,
            Intelligence = BuildTimeoutIntelligence(module)
        };
        }
        finally
        {
            KillActiveProcess();
        }
    }

    private static void RegisterActiveProcess(Process process)
    {
        lock (ActiveProcessLock)
        {
            _activeProcess = process;
        }
    }

    private static void KillActiveProcess()
    {
        lock (ActiveProcessLock)
        {
            if (_activeProcess is { HasExited: false })
            {
                try
                {
                    _activeProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignored
                }
            }

            _activeProcess = null;
        }
    }

    private static TimeSpan ResolveModuleTimeout(string module) => module switch
    {
        "quick" => TimeSpan.FromMinutes(8),
        "system" => TimeSpan.FromMinutes(12),
        "driver" => TimeSpan.FromMinutes(6),
        "audio" => TimeSpan.FromMinutes(6),
        "full" => TimeSpan.FromMinutes(20),
        _ => TimeSpan.FromMinutes(10)
    };

    private static IntelligenceSummary BuildTimeoutIntelligence(string module) => new()
    {
        Score = 72,
        Status = "주의",
        PlainSummary = $"{module} 모듈 점검이 완전히 끝나지 않았지만, 수집된 신호로 다음 단계를 계속 진행합니다.",
        RootCauses =
        [
            new RootCauseCandidate
            {
                Area = module,
                Severity = "warning",
                Evidence = "module-timeout",
                Explanation = "드라이버·오디오 점검 중 일부 명령이 지연되어 시간 제한으로 종료되었습니다.",
                Recommendation = "관리자 권한으로 다시 실행하거나 범위를 좁혀 재시도하세요.",
                Confidence = 0.7
            }
        ]
    };

    private static void RecordKnowledge(EngineEnvelope request, EngineResponse response)
    {
        try
        {
            var module = request.Params.TryGetValue("module", out var value) ? value : "system";
            var signals = response.Events.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m)).ToArray();
            var score = response.Intelligence?.Score ?? 0;
            KnowledgeService.Shared.RecordEngineRun(module, response.Status, score, signals, response.Intelligence, 0);
        }
        catch
        {
            // Knowledge persistence must never crash engine bridge calls.
        }
    }

    private static string ResolveCorePath() => RuntimePaths.ResolveCoreEnginePath();

    private static EngineResponse DemoResponse(EngineEnvelope request, string status, string message, Action<EngineEvent>? onEvent)
    {
        var module = request.Params.TryGetValue("module", out var value) ? value : "system";
        var demoEvents = new[]
        {
            new EngineEvent { Type = "stage", Module = module, Progress = 10, Message = "엔진 브리지 준비" },
            new EngineEvent { Type = "stage", Module = module, Progress = 45, Message = "모듈 파이프라인 구성" },
            new EngineEvent { Type = "stage", Module = module, Progress = 80, Message = "인텔리전스 요약 생성" }
        };

        foreach (var evt in demoEvents)
        {
            onEvent?.Invoke(evt);
        }

        return new EngineResponse
        {
            RequestId = request.Id,
            Status = status,
            Message = message + " / 현재는 UI-브리지 검증용 데모 응답을 표시합니다.",
            Events = demoEvents,
            Intelligence = new IntelligenceSummary
            {
                Score = 86,
                Status = "주의",
                PlainSummary = "Core가 아직 빌드되지 않아 데모 응답입니다. 실제 Windows 11 빌드 후 Rust Core와 연결됩니다.",
                RootCauses =
                [
                    new RootCauseCandidate
                    {
                        Area = module,
                        Severity = "info",
                        Evidence = "core executable missing",
                        Explanation = "앱 UI는 정상적으로 요청을 구성했지만 Core 실행 파일이 아직 없습니다.",
                        Recommendation = "scripts/build.ps1로 Rust Core를 빌드한 뒤 다시 실행하세요.",
                        Confidence = 0.8
                    }
                ],
                Actions =
                [
                    new ActionPlanItem
                    {
                        Priority = "1",
                        Area = "Build",
                        Action = "Rust Core 빌드",
                        Reason = "실제 진단 엔진 연결을 위해 필요합니다.",
                        Risk = "낮음"
                    }
                ]
            }
        };
    }
}
