using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Services;

public sealed class RepairHelperClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<RepairHelperResponse> SendAsync(RepairHelperRequest request, CancellationToken cancellationToken)
    {
        var helperPath = ResolveHelperPath();

        if (!File.Exists(helperPath))
        {
            return new RepairHelperResponse
            {
                Id = request.Id,
                Status = "helper-not-found",
                Message = $"RepairHelper 실행 파일을 찾지 못했습니다: {helperPath}"
            };
        }

        var pipeName = "spd-repair-" + Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        request = request with { Nonce = nonce };

        using var server = CreatePipeServer(pipeName);

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            Arguments = $"--pipe {pipeName}",
            UseShellExecute = true,
            Verb = request.DryRun ? "" : "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            return new RepairHelperResponse
            {
                Id = request.Id,
                Status = "helper-launch-failed",
                Message = $"RepairHelper 실행 실패: {ex.Message}"
            };
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(7));

        await server.WaitForConnectionAsync(timeout.Token);

        var payload = JsonSerializer.Serialize(request, JsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(payload);
        await server.WriteAsync(bytes, timeout.Token);
        await server.FlushAsync(timeout.Token);

        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
        var line = await reader.ReadLineAsync(timeout.Token);

        if (string.IsNullOrWhiteSpace(line))
        {
            return new RepairHelperResponse
            {
                Id = request.Id,
                Status = "empty",
                Message = "RepairHelper 응답이 비어 있습니다."
            };
        }

        var response = JsonSerializer.Deserialize<RepairHelperResponse>(line, JsonOptions)
            ?? new RepairHelperResponse
            {
                Id = request.Id,
                Status = "parse-failed",
                Message = "RepairHelper 응답 파싱에 실패했습니다."
            };

        if (!string.Equals(response.Nonce, nonce, StringComparison.Ordinal))
        {
            return response with
            {
                Status = "nonce-mismatch",
                Message = "RepairHelper 응답 nonce가 요청 nonce와 일치하지 않아 차단했습니다."
            };
        }

        KnowledgeService.Shared.RecordRepairOutcome(
            request.Action,
            request.Action,
            request.DryRun,
            response.Status,
            response.ExitCode ?? 0,
            response.Message);

        return response;
    }

    private static NamedPipeServerStream CreatePipeServer(string pipeName)
    {
        if (OperatingSystem.IsWindows())
        {
            var security = new PipeSecurity();

            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser is not null)
            {
                security.AddAccessRule(new PipeAccessRule(
                    currentUser,
                    PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                    AccessControlType.Allow));
            }

            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096,
                pipeSecurity: security);
        }

        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    private static string ResolveHelperPath() =>
        RuntimePaths.ResolveRepairHelperPath();
}
