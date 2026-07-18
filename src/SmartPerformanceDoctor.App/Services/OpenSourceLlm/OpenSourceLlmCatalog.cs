namespace SmartPerformanceDoctor.App.Services.OpenSourceLlm;

public static class OpenSourceLlmCatalog
{
    public const string ModelFileName = "SmolLM2-360M-Instruct-Q4_K_M.gguf";

    public const string ModelDownloadUrl =
        "https://huggingface.co/lmstudio-community/SmolLM2-360M-Instruct-GGUF/resolve/main/SmolLM2-360M-Instruct-Q4_K_M.gguf";

    public const string RunnerFileName = "llama-cli.exe";

    /// <summary>최신 win-cpu 바이너리 (릴리즈 태그는 EnsureRunnerAsync에서 API로 보정 가능).</summary>
    public const string LlamaCppZipUrl =
        "https://github.com/ggml-org/llama.cpp/releases/download/b9870/llama-b9870-bin-win-cpu-x64.zip";

    public static readonly string[] RunnerExecutableNames =
    {
        "llama-cli.exe",
        "llama.exe",
        "main.exe"
    };
}