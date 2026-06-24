namespace SmartPerformanceDoctor.App.Models;

public sealed record ReleaseArtifact(
    string Name,
    string Kind,
    string Path,
    bool Exists,
    long SizeBytes,
    string Sha256,
    string Status,
    string Message);
