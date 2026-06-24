using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.Contracts;

var argsList = args.ToList();
if (argsList.Contains("--pipe"))
{
    var pipeIndex = argsList.IndexOf("--pipe");
    if (pipeIndex + 1 >= argsList.Count)
    {
        Console.Error.WriteLine("missing pipe name");
        return 2;
    }

    return await RunPipeModeAsync(argsList[pipeIndex + 1]);
}

if (argsList.Contains("--rebuild-baseline"))
{
    var rootIndex = argsList.IndexOf("--install-root");
    var versionIndex = argsList.IndexOf("--version");
    if (rootIndex + 1 >= argsList.Count || versionIndex + 1 >= argsList.Count)
    {
        Console.Error.WriteLine("usage: --rebuild-baseline --install-root <path> --version <ver>");
        return 2;
    }

    AegisBaselineService.RebuildBaseline(argsList[rootIndex + 1], argsList[versionIndex + 1]);
    Console.WriteLine("ok");
    return 0;
}

Console.Error.WriteLine("usage: --pipe <name> | --rebuild-baseline --install-root <path> --version <ver>");
return 2;

static async Task<int> RunPipeModeAsync(string pipeName)
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };

    try
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(7000);

        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line))
        {
            await WriteResponseAsync(client, new AegisRecoveryResponse
            {
                Status = "empty",
                Message = "요청이 비어 있습니다.",
                ExitCode = 3
            }, jsonOptions);
            return 3;
        }

        var request = JsonSerializer.Deserialize<AegisRecoveryRequest>(line, jsonOptions);
        if (request is null)
        {
            await WriteResponseAsync(client, new AegisRecoveryResponse
            {
                Status = "parse-failed",
                Message = "요청 파싱 실패",
                ExitCode = 4
            }, jsonOptions);
            return 4;
        }

        var (restored, status, message) = AegisElevatedRepairService.Execute(
            request.Action,
            request.InstallRoot,
            string.IsNullOrWhiteSpace(request.StagingDirectory) ? null : request.StagingDirectory,
            request.Version);

        await WriteResponseAsync(client, new AegisRecoveryResponse
        {
            Id = request.Id,
            Status = status,
            Message = message,
            Restored = restored,
            ExitCode = status is "ok" or "noop" ? 0 : 1,
            Elevated = true,
            Nonce = request.Nonce
        }, jsonOptions);

        return status is "ok" or "noop" ? 0 : 1;
    }
    catch (Exception ex)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(2000);
            var payload = JsonSerializer.Serialize(new AegisRecoveryResponse
            {
                Status = "helper-error",
                Message = ex.Message,
                ExitCode = 5
            }, jsonOptions) + "\n";
            await client.WriteAsync(Encoding.UTF8.GetBytes(payload));
        }
        catch
        {
            // Best-effort error response.
        }

        return 5;
    }
}

static async Task WriteResponseAsync(NamedPipeClientStream client, AegisRecoveryResponse response, JsonSerializerOptions jsonOptions)
{
    var payload = JsonSerializer.Serialize(response, jsonOptions) + "\n";
    await client.WriteAsync(Encoding.UTF8.GetBytes(payload));
    await client.FlushAsync();
}