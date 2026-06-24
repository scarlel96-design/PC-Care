namespace SmartPerformanceDoctor.App.Models;

public sealed record UpdateChannelInfo(
    string CurrentVersion,
    string Channel,
    string ManifestPath,
    string LatestVersion,
    string Status,
    string Message);
