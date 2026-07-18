using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Models.Update;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdateChannelService
{
    private readonly UpdateHistoryStore _history = new();

    public string CurrentVersion => AppInfo.Version;

    public UpdateChannelInfo BuildStatus(string? selectedPackagePath = null)
    {
        if (!string.IsNullOrWhiteSpace(selectedPackagePath) && File.Exists(selectedPackagePath))
        {
            var inspection = new UpdatePackageInspector().Inspect(selectedPackagePath, CurrentVersion);
            return new UpdateChannelInfo(
                CurrentVersion,
                inspection.Manifest?.Channel ?? AppInfo.Channel,
                selectedPackagePath,
                inspection.Manifest?.ToVersion ?? CurrentVersion,
                inspection.Status,
                inspection.Message);
        }

        var inboxLatest = FindLatestInboxPackage();
        if (inboxLatest is not null)
        {
            var inspection = new UpdatePackageInspector().Inspect(inboxLatest, CurrentVersion);
            return new UpdateChannelInfo(
                CurrentVersion,
                inspection.Manifest?.Channel ?? AppInfo.Channel,
                inboxLatest,
                inspection.Manifest?.ToVersion ?? CurrentVersion,
                inspection.Status,
                inspection.Message);
        }

        return new UpdateChannelInfo(
            CurrentVersion,
            AppInfo.Channel,
            "",
            CurrentVersion,
            "IDLE",
            "업데이트 파일(.spdup)을 선택하거나 GitHub에서 확인·다운로드할 수 있습니다.");
    }

    public IReadOnlyList<UpdateHistoryEntry> LoadHistory() => _history.LoadRecent();

    private static string? FindLatestInboxPackage()
    {
        UpdatePaths.EnsureLayout();
        return Directory.GetFiles(UpdatePaths.Inbox, "*.spdup")
            .Concat(Directory.GetFiles(UpdatePaths.Inbox, "*.zip"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}