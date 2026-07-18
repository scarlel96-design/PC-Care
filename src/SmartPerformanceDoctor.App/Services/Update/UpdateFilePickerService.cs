using Microsoft.UI.Xaml;
using SmartPerformanceDoctor.App.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinForms = System.Windows.Forms;

namespace SmartPerformanceDoctor.App.Services.Update;

public static class UpdateFilePickerService
{
    public static async Task<string?> PickUpdatePackageAsync(Window? window = null)
    {
        var targetWindow = window ?? App.Shell;
        if (targetWindow is not null)
        {
            try
            {
                var path = await PickWithWinRtPickerAsync(targetWindow).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                CrashCaptureService.WriteCrash("update-file-picker-winrt", ex, ex.Message);
            }
        }

        try
        {
            return PickWithWinFormsDialog();
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("update-file-picker-winforms", ex, ex.Message);
            return null;
        }
    }

    private static async Task<string?> PickWithWinRtPickerAsync(Window targetWindow)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add(".spdup");
        picker.FileTypeFilter.Add(".zip");
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(targetWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private static string? PickWithWinFormsDialog()
    {
        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "업데이트 패키지 선택",
            Filter = "PC Care 업데이트 (*.spdup)|*.spdup|ZIP 압축 (*.zip)|*.zip|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.FileName : null;
    }
}