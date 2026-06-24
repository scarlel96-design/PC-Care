using System.Text.Json;
using SmartPerformanceDoctor.Data;

namespace SmartPerformanceDoctor.App.Services;

public static class LearningCorpusSeeder
{
    private const string MarkerFile = "learning_corpus_seeded_v3";
    private const int CorpusTargetCount = 52_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void SeedIfNeeded(KnowledgeDatabase database, string rulesRoot)
    {
        var marker = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "data",
            MarkerFile);

        if (File.Exists(marker) && database.GetLearningCorpusCount() >= CorpusTargetCount / 2)
        {
            return;
        }

        var patterns = BuildPatterns(rulesRoot);
        database.BulkSeedLearningPatterns(patterns);

        var corpus = BuildCorpusRecords(rulesRoot);
        foreach (var batch in corpus.Chunk(500))
        {
            database.BulkSeedLearningCorpus(batch);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, $"{DateTimeOffset.Now:o}|patterns={patterns.Count}|corpus={corpus.Count}");
    }

    private static IReadOnlyList<string> BuildPatterns(string rulesRoot)
    {
        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modules = new[] { "quick", "system", "driver", "audio", "full" };
        var actions = new[]
        {
            "audio_restart_stack",
            "audio_scan_devices",
            "driver_check_problem_devices",
            "pnputil_scan_devices",
            "dism_checkhealth",
            "service_restart",
            "device_rescan",
            "skip_repair_healthy_path"
        };

        foreach (var module in modules)
        {
            foreach (var action in actions)
            {
                patterns.Add($"{module}:{action}");
            }
        }

        TryAddRuleKeys(patterns, Path.Combine(rulesRoot, "driver_problem_codes.json"));
        TryAddRuleKeys(patterns, Path.Combine(rulesRoot, "audio_rules.json"));
        TryAddRuleKeys(patterns, Path.Combine(rulesRoot, "system_rules.json"));

        for (var code = 1; code <= 56; code++)
        {
            patterns.Add($"driver:problem_code_{code}");
            patterns.Add($"driver:code_{code}:restart-rescan");
            patterns.Add($"driver:code_{code}:reinstall-official");
        }

        for (var i = 0; i < 500; i++)
        {
            patterns.Add($"audio:symptom_nosound_variant_{i}");
            patterns.Add($"audio:endpoint_health_variant_{i}");
            patterns.Add($"driver:conflict_variant_{i}");
            patterns.Add($"system:service_degraded_variant_{i}");
        }

        return patterns.ToArray();
    }

    private static IReadOnlyList<LearningPatternRecord> BuildCorpusRecords(string rulesRoot)
    {
        var records = new List<LearningPatternRecord>(CorpusTargetCount);
        var audioSymptoms = new[]
        {
            "소리 없음", "음소거 상태", "기본 장치 없음", "블루투스 오디오 끊김",
            "USB 오디오 미인식", "서비스 중지", "엔드포인트 오류", "재생 경로 정상"
        };
        var driverSymptoms = new[]
        {
            "문제 장치 없음", "코드 28 드라이버 없음", "코드 31 장치 실패", "코드 39 손상",
            "중복 드라이버", "서명 없는 드라이버", "PnP 충돌", "정상 인벤토리"
        };
        var systemSymptoms = new[]
        {
            "디스크 여유 부족", "메모리 압박", "서비스 지연", "이벤트 로그 경고",
            "시스템 파일 불일치", "업데이트 보류", "전원 정책", "정상 베이스라인",
            "시작 항목 과다", "DNS 지연", "브라우저 캐시 과다", "임시 폴더 비대",
            "페이지 파일 비정상", "전달 최적화 캐시", "시각 효과 최대", "인덱서 중지"
        };

        SeedModuleVariants(records, "audio", audioSymptoms, 14_000);
        SeedModuleVariants(records, "driver", driverSymptoms, 14_000);
        SeedModuleVariants(records, "system", systemSymptoms, 16_000);
        SeedCrossModuleVariants(records, 8_000);

        TryImportRuleCorpus(records, Path.Combine(rulesRoot, "audio_rules.json"), "audio");
        TryImportRuleCorpus(records, Path.Combine(rulesRoot, "system_rules.json"), "system");

        return records;
    }

    private static void SeedModuleVariants(
        List<LearningPatternRecord> records,
        string module,
        IReadOnlyList<string> symptoms,
        int target)
    {
        var vendors = new[] { "Realtek", "NVIDIA", "Intel", "AMD", "Microsoft", "Bluetooth", "USB", "Generic" };
        var actions = module switch
        {
            "audio" => new[] { "audio_restart_stack", "audio_scan_devices", "skip_repair_healthy_path", "set_default_endpoint" },
            "driver" => new[] { "device_rescan", "driver_check_problem_devices", "skip_repair_healthy_path", "pnputil_scan_devices" },
            _ => new[] { "dism_checkhealth", "service_restart", "skip_repair_healthy_path" }
        };

        for (var i = 0; i < target; i++)
        {
            var symptom = symptoms[i % symptoms.Count];
            var vendor = vendors[i % vendors.Length];
            var repairNeeded = !symptom.Contains("정상") && !symptom.Contains("없음");
            var key = $"{module}:corpus:{i:D5}";
            var signals = new[]
            {
                $"RepairNeeded={repairNeeded.ToString().ToLowerInvariant()}",
                $"vendor={vendor}",
                $"symptom={symptom}",
                $"scanDepth=precision_v2",
                $"variant={i % 97}"
            };
            var chosenActions = repairNeeded
                ? actions.Where(a => !a.Contains("skip")).Take(2).ToArray()
                : new[] { "skip_repair_healthy_path", "diagnostic_only" };

            var context = BuildContextBlob(module, symptom, vendor, repairNeeded, i);
            records.Add(new LearningPatternRecord(
                key,
                "precision_scan",
                module,
                symptom,
                JsonSerializer.Serialize(signals, JsonOptions),
                JsonSerializer.Serialize(chosenActions, JsonOptions),
                context,
                repairNeeded ? 0.68 : 0.92));
        }
    }

    private static void SeedCrossModuleVariants(List<LearningPatternRecord> records, int target)
    {
        for (var i = 0; i < target; i++)
        {
            var audioIssue = i % 3 == 0;
            var driverIssue = i % 5 == 0;
            var key = $"full:fusion:{i:D5}";
            var symptom = audioIssue && driverIssue
                ? "오디오+드라이버 복합 이상"
                : audioIssue
                    ? "오디오 경로 이상"
                    : driverIssue
                        ? "드라이버 충돌 후보"
                        : "복합 점검 정상";

            var repairNeeded = audioIssue || driverIssue;
            var signals = new[]
            {
                $"RepairNeeded={repairNeeded.ToString().ToLowerInvariant()}",
                $"audioSignal={audioIssue}",
                $"driverSignal={driverIssue}",
                "scope=full"
            };
            var actions = repairNeeded
                ? new[] { "targeted_repair_only", "post_verify_rescan" }
                : new[] { "skip_repair_healthy_path" };

            records.Add(new LearningPatternRecord(
                key,
                "fusion_inference",
                "full",
                symptom,
                JsonSerializer.Serialize(signals, JsonOptions),
                JsonSerializer.Serialize(actions, JsonOptions),
                BuildContextBlob("full", symptom, "CrossModule", repairNeeded, i),
                repairNeeded ? 0.7 : 0.9));
        }
    }

    private static string BuildContextBlob(string module, string symptom, string vendor, bool repairNeeded, int variant)
    {
        var guidance = repairNeeded
            ? "정밀 스캔에서 실제 이상 신호가 확인되어 필요한 조치만 순서대로 적용합니다."
            : "정밀 스캔 결과 이상 신호가 없어 복구를 건너뛰고 진단 보고서만 생성합니다.";

        return JsonSerializer.Serialize(new
        {
            module,
            symptom,
            vendor,
            repairNeeded,
            variant,
            guidance,
            evidenceHints = new[]
            {
                $"{module} preflight snapshot",
                $"{module} event correlation (21d)",
                $"{module} repair plan gate"
            },
            narrative = string.Join(' ', Enumerable.Repeat(
                $"{symptom}에 대한 {vendor} 장치/서비스 상관 분석 패턴 #{variant}. " +
                "엔드포인트·서비스·PnP·이벤트 로그를 교차 검증하여 무작위 복구를 방지합니다.",
                12))
        }, JsonOptions);
    }

    private static void TryImportRuleCorpus(List<LearningPatternRecord> records, string path, string module)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var index = 0;
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                var key = $"rule:{Path.GetFileNameWithoutExtension(path)}:{entry.Name}:{index++}";
                records.Add(new LearningPatternRecord(
                    key,
                    "rule_engine",
                    module,
                    entry.Name,
                    entry.Value.GetRawText(),
                    "[]",
                    entry.Value.GetRawText(),
                    0.75));
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void TryAddRuleKeys(HashSet<string> patterns, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var key in doc.RootElement.EnumerateObject().Select(p => p.Name))
            {
                patterns.Add($"rule:{Path.GetFileNameWithoutExtension(path)}:{key}");
            }
        }
        catch
        {
            // ignored
        }
    }
}