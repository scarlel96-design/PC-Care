using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.OpenSourceLlm;

public sealed class OpenSourceLlmDownloadService
{
    private static readonly HttpClient Http = CreateHttpClient();

    public string ModelsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartPerformanceDoctor",
        "models");

    public string ModelPath => Path.Combine(ModelsDirectory, OpenSourceLlmCatalog.ModelFileName);

    public string RunnerPath => ResolveRunnerPath();

    public bool IsReady => File.Exists(ModelPath) && File.Exists(ResolveRunnerPath());

    public string ResolveRunnerPath()
    {
        foreach (var name in OpenSourceLlmCatalog.RunnerExecutableNames)
        {
            var candidate = Path.Combine(ModelsDirectory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(ModelsDirectory, OpenSourceLlmCatalog.RunnerFileName);
    }

    public async Task EnsureModelAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ModelsDirectory);
        if (File.Exists(ModelPath))
        {
            progress?.Report("모델이 이미 있습니다.");
            return;
        }

        progress?.Report("경량 AI 모델 다운로드 중…");
        await DownloadFileAsync(OpenSourceLlmCatalog.ModelDownloadUrl, ModelPath, progress, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report("모델 다운로드 완료.");
    }

    public async Task EnsureRunnerAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ModelsDirectory);
        if (File.Exists(RunnerPath))
        {
            progress?.Report("실행기가 이미 있습니다.");
            return;
        }

        progress?.Report("llama.cpp 실행기 다운로드 중…");
        var zipUrl = await ResolveLlamaCppZipUrlAsync(cancellationToken).ConfigureAwait(false);
        var zipPath = Path.Combine(ModelsDirectory, "llama-cpp.zip");
        await DownloadFileAsync(zipUrl, zipPath, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report("실행기 압축 해제 중…");
        var extractDir = Path.Combine(ModelsDirectory, "_llama_extract");
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, recursive: true);
        }

        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        string? runner = null;
        foreach (var name in OpenSourceLlmCatalog.RunnerExecutableNames)
        {
            runner = Directory.EnumerateFiles(extractDir, name, SearchOption.AllDirectories).FirstOrDefault();
            if (runner is not null)
            {
                break;
            }
        }

        if (runner is null)
        {
            throw new FileNotFoundException("압축 파일에서 llama 실행기(llama-cli.exe 등)를 찾지 못했습니다.");
        }

        CopyRunnerPayload(Path.GetDirectoryName(runner)!, ModelsDirectory);
        File.Delete(zipPath);

        try
        {
            Directory.Delete(extractDir, recursive: true);
        }
        catch
        {
            // Cleanup is best-effort.
        }

        progress?.Report("실행기 준비 완료.");
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(45)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"PCCare/{AppInfo.BuildVersion} (local-ai-download)");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return http;
    }

    private static async Task<string> ResolveLlamaCppZipUrlAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(
                    "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest",
                    cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains("win-cpu", StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        return asset.GetProperty("browser_download_url").GetString()
                               ?? OpenSourceLlmCatalog.LlamaCppZipUrl;
                    }
                }
            }
        }
        catch
        {
            // fall back to pinned URL
        }

        return OpenSourceLlmCatalog.LlamaCppZipUrl;
    }

    private static void CopyRunnerPayload(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
            }
        }
    }

    private static async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = destination + ".partial";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        var requestUri = url.Contains('?', StringComparison.Ordinal)
            ? url
            : url + (url.Contains("huggingface.co", StringComparison.OrdinalIgnoreCase) ? "?download=true" : "");

        using (var response = await Http.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"다운로드 실패 ({(int)response.StatusCode}): {Truncate(body, 200)}");
            }
            var total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var file = File.Create(tempPath);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readTotal += read;
                if (total > 0)
                {
                    var pct = (int)(readTotal * 100 / total.Value);
                    progress?.Report($"다운로드 {pct}%");
                }
            }
        }

        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(tempPath, destination);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}