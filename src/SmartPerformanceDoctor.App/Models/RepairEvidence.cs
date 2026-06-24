namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairEvidence(
    string Kind,
    string Source,
    string Before,
    string After,
    string Verdict,
    string Detail);
