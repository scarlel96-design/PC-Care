using Microsoft.UI.Xaml;

namespace SmartPerformanceDoctor.App.Services.Pickers;

public enum PickerStatus
{
    Success,
    Cancelled,
    Failed
}

public enum PickerStartLocation
{
    Documents,
    Downloads,
    Desktop
}

public sealed record PickerRequest(
    string Feature,
    string Title,
    string CommitButtonText,
    PickerStartLocation StartLocation = PickerStartLocation.Documents,
    IReadOnlyList<string>? FileTypeFilter = null,
    string? SuggestedFileName = null,
    string? DefaultFileExtension = null);

public sealed record PickerResult<T>(
    PickerStatus Status,
    T? Value,
    string UserMessage,
    int? HResult = null,
    string? TrackingId = null)
{
    public bool IsSuccess => Status == PickerStatus.Success;
    public bool IsCancelled => Status == PickerStatus.Cancelled;
    public bool IsFailed => Status == PickerStatus.Failed;

    public static PickerResult<T> Success(T value, string message) =>
        new(PickerStatus.Success, value, message);

    public static PickerResult<T> Cancelled(string message) =>
        new(PickerStatus.Cancelled, default, message);

    public static PickerResult<T> Failed(string message, int? hResult, string trackingId) =>
        new(PickerStatus.Failed, default, message, hResult, trackingId);
}

public interface IPathPickerService
{
    Task<PickerResult<string>> PickSingleFileAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default);

    Task<PickerResult<IReadOnlyList<string>>> PickMultipleFilesAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default);

    Task<PickerResult<string>> PickFolderAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default);

    Task<PickerResult<string>> PickSaveFileAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWindowProvider
{
    Window? CurrentWindow { get; }
}

internal sealed class AppWindowProvider(Func<Window?> accessor) : IWindowProvider
{
    public Window? CurrentWindow => accessor();
}

public sealed class PickerOperationGate
{
    private int _active;

    public bool IsActive => Volatile.Read(ref _active) == 1;

    public bool TryEnter() => Interlocked.CompareExchange(ref _active, 1, 0) == 0;

    public void Exit() => Interlocked.Exchange(ref _active, 0);
}