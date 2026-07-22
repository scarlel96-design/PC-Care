using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace SmartPerformanceDoctor.App.Services.Pickers;

public sealed class PathPickerService(IWindowProvider windowProvider) : IPathPickerService
{
    public static IPathPickerService Shared { get; } =
        new PathPickerService(new AppWindowProvider(() => App.Shell));

    public async Task<PickerResult<string>> PickSingleFileAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PickerResult<string>.Cancelled("파일 선택을 취소했습니다.");
        }
        var validation = ValidateOwner(owner ?? windowProvider.CurrentWindow, request, "PickSingleFile");
        if (validation.Failure is not null)
        {
            return validation.Failure;
        }

        try
        {
            var picker = CreateFileOpenPicker(validation.WindowId, request);
            var result = await picker.PickSingleFileAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result is null
                ? PickerResult<string>.Cancelled("파일 선택을 취소했습니다.")
                : PickerResult<string>.Success(result.Path, "파일 선택을 완료했습니다.");
        }
        catch (OperationCanceledException)
        {
            return PickerResult<string>.Cancelled("파일 선택을 취소했습니다.");
        }
        catch (Exception ex)
        {
            return PickerDiagnostics.Failure<string>(request.Feature, "PickSingleFile", ex, true, true);
        }
    }

    public async Task<PickerResult<IReadOnlyList<string>>> PickMultipleFilesAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PickerResult<IReadOnlyList<string>>.Cancelled("파일 선택을 취소했습니다.");
        }
        var validation = ValidateOwner<IReadOnlyList<string>>(
            owner ?? windowProvider.CurrentWindow,
            request,
            "PickMultipleFiles");
        if (validation.Failure is not null)
        {
            return validation.Failure;
        }

        try
        {
            var picker = CreateFileOpenPicker(validation.WindowId, request);
            var results = await picker.PickMultipleFilesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (results is null || results.Count == 0)
            {
                return PickerResult<IReadOnlyList<string>>.Cancelled("파일 선택을 취소했습니다.");
            }

            IReadOnlyList<string> paths = results.Select(item => item.Path).ToArray();
            return PickerResult<IReadOnlyList<string>>.Success(paths, $"파일 {paths.Count}개를 선택했습니다.");
        }
        catch (OperationCanceledException)
        {
            return PickerResult<IReadOnlyList<string>>.Cancelled("파일 선택을 취소했습니다.");
        }
        catch (Exception ex)
        {
            return PickerDiagnostics.Failure<IReadOnlyList<string>>(
                request.Feature,
                "PickMultipleFiles",
                ex,
                true,
                true);
        }
    }

    public async Task<PickerResult<string>> PickFolderAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PickerResult<string>.Cancelled("폴더 선택을 취소했습니다.");
        }
        var validation = ValidateOwner(owner ?? windowProvider.CurrentWindow, request, "PickFolder");
        if (validation.Failure is not null)
        {
            return validation.Failure;
        }

        try
        {
            var picker = new FolderPicker(validation.WindowId)
            {
                Title = request.Title,
                CommitButtonText = request.CommitButtonText,
                SuggestedStartLocation = MapLocation(request.StartLocation),
                ViewMode = PickerViewMode.List
            };
            var result = await picker.PickSingleFolderAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result is null
                ? PickerResult<string>.Cancelled("폴더 선택을 취소했습니다.")
                : PickerResult<string>.Success(result.Path, "폴더 선택을 완료했습니다.");
        }
        catch (OperationCanceledException)
        {
            return PickerResult<string>.Cancelled("폴더 선택을 취소했습니다.");
        }
        catch (Exception ex)
        {
            return PickerDiagnostics.Failure<string>(request.Feature, "PickFolder", ex, true, true);
        }
    }

    public async Task<PickerResult<string>> PickSaveFileAsync(
        Window? owner,
        PickerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PickerResult<string>.Cancelled("저장 위치 선택을 취소했습니다.");
        }
        var validation = ValidateOwner(owner ?? windowProvider.CurrentWindow, request, "PickSaveFile");
        if (validation.Failure is not null)
        {
            return validation.Failure;
        }

        try
        {
            var picker = new FileSavePicker(validation.WindowId)
            {
                Title = request.Title,
                CommitButtonText = request.CommitButtonText,
                SuggestedStartLocation = MapLocation(request.StartLocation),
                SuggestedFileName = request.SuggestedFileName ?? string.Empty,
                DefaultFileExtension = request.DefaultFileExtension ?? string.Empty,
                ShowOverwritePrompt = true
            };
            var filters = NormalizeFilters(request.FileTypeFilter);
            picker.FileTypeChoices.Add("파일", filters.ToList());
            var result = await picker.PickSaveFileAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result is null
                ? PickerResult<string>.Cancelled("저장 위치 선택을 취소했습니다.")
                : PickerResult<string>.Success(result.Path, "저장 위치 선택을 완료했습니다.");
        }
        catch (OperationCanceledException)
        {
            return PickerResult<string>.Cancelled("저장 위치 선택을 취소했습니다.");
        }
        catch (Exception ex)
        {
            return PickerDiagnostics.Failure<string>(request.Feature, "PickSaveFile", ex, true, true);
        }
    }

    private static FileOpenPicker CreateFileOpenPicker(Microsoft.UI.WindowId windowId, PickerRequest request)
    {
        var picker = new FileOpenPicker(windowId)
        {
            Title = request.Title,
            CommitButtonText = request.CommitButtonText,
            SuggestedStartLocation = MapLocation(request.StartLocation),
            ViewMode = PickerViewMode.List
        };
        foreach (var filter in NormalizeFilters(request.FileTypeFilter))
        {
            picker.FileTypeFilter.Add(filter);
        }

        return picker;
    }

    private static string[] NormalizeFilters(IReadOnlyList<string>? filters) =>
        filters is null || filters.Count == 0
            ? ["*"]
            : filters.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static PickerLocationId MapLocation(PickerStartLocation location) => location switch
    {
        PickerStartLocation.Downloads => PickerLocationId.Downloads,
        PickerStartLocation.Desktop => PickerLocationId.Desktop,
        _ => PickerLocationId.DocumentsLibrary
    };

    private static OwnerValidation<T> ValidateOwner<T>(Window? owner, PickerRequest request, string operation)
    {
        if (owner is null)
        {
            var ex = new InvalidOperationException("선택기를 소유할 창을 찾을 수 없습니다.");
            return new(default, PickerDiagnostics.Failure<T>(request.Feature, operation, ex, false, false));
        }

        var uiThread = owner.DispatcherQueue?.HasThreadAccess == true;
        if (!uiThread)
        {
            var ex = new InvalidOperationException("선택기는 UI 스레드에서 호출해야 합니다.");
            return new(default, PickerDiagnostics.Failure<T>(request.Feature, operation, ex, false, false));
        }

        try
        {
            var appWindow = owner.AppWindow;
            var windowId = appWindow.Id;
            if (windowId.Value == 0)
            {
                throw new InvalidOperationException("소유 창의 WindowId가 아직 초기화되지 않았습니다.");
            }

            return new(windowId, null);
        }
        catch (Exception ex)
        {
            return new(default, PickerDiagnostics.Failure<T>(request.Feature, operation, ex, false, true));
        }
    }

    private static OwnerValidation<string> ValidateOwner(Window? owner, PickerRequest request, string operation) =>
        ValidateOwner<string>(owner, request, operation);

    private readonly record struct OwnerValidation<T>(
        Microsoft.UI.WindowId WindowId,
        PickerResult<T>? Failure);
}
