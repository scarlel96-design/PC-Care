using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SmartPerformanceDoctor.App.Services.Security;

public static class SecureVaultPickerService
{
    public static async Task<string?> PickFileAsync(Window? window = null)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        return await PickAsync(picker, window);
    }

    public static async Task<string?> PickFolderAsync(Window? window = null)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        return await PickFolderAsync(picker, window);
    }

    public static async Task<string?> PickExportFolderAsync(Window? window = null)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        return await PickFolderAsync(picker, window);
    }

    private static async Task<string?> PickAsync(FileOpenPicker picker, Window? window)
    {
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

    private static async Task<string?> PickFolderAsync(FolderPicker picker, Window? window)
    {
        var targetWindow = window ?? App.Shell;
        if (targetWindow is null)
        {
            return null;
        }

        var hwnd = WindowNative.GetWindowHandle(targetWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}