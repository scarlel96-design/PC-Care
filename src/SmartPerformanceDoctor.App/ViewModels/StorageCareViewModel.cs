using System.Collections.ObjectModel;
using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.Diagnostics;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class StorageCareViewModel : ObservableObject
{
    private readonly DiagnosticLifecycle lifecycle = new();
    private bool busy;
    private string status = "사용자 임시 폴더의 파일 메타데이터만 검사합니다.";
    private string summary = "검사 전 · 확보 공간 미측정";
    private string preview = "후보를 선택한 뒤 Dry Run을 열어 근거와 제한을 확인하세요.";
    public ObservableCollection<CleanupCandidate> Candidates { get; } = [];
    public ObservableCollection<string> Drives { get; } = [];
    public bool IsBusy { get => busy; private set { Set(ref busy, value); OnPropertyChanged(nameof(CanStart)); } }
    public bool CanStart => !IsBusy;
    public string Status { get => status; private set => Set(ref status, value); }
    public string Summary { get => summary; private set => Set(ref summary, value); }
    public string Preview { get => preview; private set => Set(ref preview, value); }
    public async Task ScanAsync()
    {
        if (!lifecycle.TryBegin(out var lease) || lease is null) return;
        IsBusy = true; Candidates.Clear(); Drives.Clear();
        Status = "파일 신원·소유권·크기 검사 중 · 최대 512개 / 깊이 2 / 5초";
        try
        {
            var result = await Task.Run(() => StorageInventory.CaptureUserTemp(lease.Token), lease.Token);
            if (!lifecycle.IsCurrent(lease)) return;
            foreach (var drive in result.Drives)
                Drives.Add($"{drive.Drive} · 전체 {Format(drive.CapacityBytes)} · 사용 가능 {Format(drive.AvailableBytes)} · 전체 여유 {Format(drive.TotalFreeBytes)} · {(drive.Status == "Measured" ? "측정 완료" : "측정 제한")}");
            foreach (var row in result.Candidates) Candidates.Add(row);
            Summary = $"논리 크기 {result.MeasuredLogicalBytes:N0} B · 할당 크기 {(result.MeasuredAllocatedBytes is long allocated ? allocated.ToString("N0") + " B" : "미확인")} · 제외 {result.ExcludedCount}개 · {result.ElapsedMs:N0} ms";
            Status = result.Limitation;
            Preview = "격리 0 B · 복원 가능 0 B · 최종 삭제 0 B · 실제 확보 공간 미측정";
        }
        catch (OperationCanceledException) { if (lifecycle.IsCurrent(lease)) Status = "검사를 중단했습니다. 완료되지 않은 결과는 사용하지 않습니다."; }
        catch (Exception) { if (lifecycle.IsCurrent(lease)) Status = "검사 접근 오류 · 재검사 필요 · 정상 상태로 판단하지 않습니다."; }
        finally { lifecycle.Complete(lease); IsBusy = false; }
    }
    public async Task PreviewAsync(IReadOnlyList<CleanupCandidate> selected)
    {
        if (!lifecycle.TryBegin(out var lease) || lease is null) return;
        IsBusy = true;
        try
        {
            var result = await Task.Run(() => StorageInventory.DryRun(selected, lease.Token), lease.Token);
            if (lifecycle.IsCurrent(lease)) Preview = $"선택 {result.SelectedCount}개 · 재검증 후보 {result.CandidateBytes:N0} B\n"
                + string.Join("\n", result.Checks) + "\n실제 확보 공간 미측정 · 변경 0건 · 복원 계획: 격리본 신원 확인 후 원본 충돌 없이 복원";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (lifecycle.IsCurrent(lease)) Preview = "대상 재검증 오류 · 실행 차단"; }
        finally { lifecycle.Complete(lease); IsBusy = false; }
    }
    public void Stop() => lifecycle.Cancel();
    public void Leave() => lifecycle.Cancel(invalidate: true);
    private static string Format(long? bytes) => bytes is long n ? $"{n / 1073741824d:N1} GiB" : "미확인";
}
