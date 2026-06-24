using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.Contracts;

public sealed record EngineEnvelope
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("method")]
    public string Method { get; init; } = "";

    [JsonPropertyName("params")]
    public Dictionary<string, string> Params { get; init; } = new();
}

public sealed record EngineFrame
{
    [JsonPropertyName("frameType")]
    public string FrameType { get; init; } = "";

    [JsonPropertyName("event")]
    public EngineEvent? Event { get; init; }

    [JsonPropertyName("response")]
    public EngineResponse? Response { get; init; }
}

public sealed record EngineEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("module")]
    public string Module { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("progress")]
    public int Progress { get; init; }

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}

public sealed record EngineResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("events")]
    public IReadOnlyList<EngineEvent> Events { get; init; } = Array.Empty<EngineEvent>();

    [JsonPropertyName("intelligence")]
    public IntelligenceSummary? Intelligence { get; init; }

    [JsonPropertyName("reportPath")]
    public string? ReportPath { get; init; }

    [JsonPropertyName("htmlReportPath")]
    public string? HtmlReportPath { get; init; }

    [JsonPropertyName("jsonReportPath")]
    public string? JsonReportPath { get; init; }
}

public sealed record IntelligenceSummary
{
    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("plainSummary")]
    public string PlainSummary { get; init; } = "";

    [JsonPropertyName("rootCauses")]
    public IReadOnlyList<RootCauseCandidate> RootCauses { get; init; } = Array.Empty<RootCauseCandidate>();

    [JsonPropertyName("actions")]
    public IReadOnlyList<ActionPlanItem> Actions { get; init; } = Array.Empty<ActionPlanItem>();
}

public sealed record RootCauseCandidate
{
    [JsonPropertyName("area")]
    public string Area { get; init; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "";

    [JsonPropertyName("evidence")]
    public string Evidence { get; init; } = "";

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = "";

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; init; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
}

public sealed record ActionPlanItem
{
    [JsonPropertyName("priority")]
    public string Priority { get; init; } = "";

    [JsonPropertyName("area")]
    public string Area { get; init; } = "";

    [JsonPropertyName("action")]
    public string Action { get; init; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";

    [JsonPropertyName("risk")]
    public string Risk { get; init; } = "";
}


public sealed record RepairHelperRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("action")]
    public string Action { get; init; } = "";

    [JsonPropertyName("target")]
    public string Target { get; init; } = "";

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; init; } = true;

    [JsonPropertyName("riskAccepted")]
    public bool RiskAccepted { get; init; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = "";
}

public sealed record RepairHelperResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }

    [JsonPropertyName("stdout")]
    public string Stdout { get; init; } = "";

    [JsonPropertyName("stderr")]
    public string Stderr { get; init; } = "";

    [JsonPropertyName("elevated")]
    public bool Elevated { get; init; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = "";

    [JsonPropertyName("logPath")]
    public string LogPath { get; init; } = "";
}

public sealed record AegisRecoveryRequest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("action")]
    public string Action { get; init; } = "";

    [JsonPropertyName("installRoot")]
    public string InstallRoot { get; init; } = "";

    [JsonPropertyName("stagingDirectory")]
    public string StagingDirectory { get; init; } = "";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("elevated")]
    public bool Elevated { get; init; } = true;

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = "";
}

public sealed record AegisRecoveryResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("restored")]
    public int Restored { get; init; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }

    [JsonPropertyName("elevated")]
    public bool Elevated { get; init; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = "";
}
