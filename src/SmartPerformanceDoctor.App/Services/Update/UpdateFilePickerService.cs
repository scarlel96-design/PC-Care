using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SmartPerformanceDoctor.App.Services.Update;

public static class UpdateFilePickerService
{
    public static async Task<string?> PickUpdatePackageAsync(Window? window = null)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add(".spdup");
        picker.FileTypeFilter.Add(".zip");

        var targetWindow = window ?? App.Shell;
        if (targetWindow is null)
        {
            return null;
        }

        var hwnd = WindowNative.GetWindowHandle(targetWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}