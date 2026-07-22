using System.Security.Principal;
using System.Text.RegularExpressions;

namespace SmartPerformanceDoctor.App.Services.Pickers;

internal static partial class PickerDiagnostics
{
    public static PickerResult<T> Failure<T>(
        string feature,
        string operation,
        Exception exception,
        bool windowIdValid,
        bool uiThread)
    {
        var trackingId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var hResult = exception.HResult;
        var stack = RedactPaths(exception.StackTrace ?? "(호출 스택 없음)");
        var detail =
            $"Feature: {feature}\n" +
            "PickerBackend: Microsoft.Windows.Storage.Pickers\n" +
            $"Operation: {operation}\n" +
            $"HRESULT: 0x{hResult:X8}\n" +
            $"ExceptionType: {exception.GetType().FullName}\n" +
            $"Elevated: {IsElevated()}\n" +
            $"Packaged: {IsPackaged()}\n" +
            $"OS: {Environment.OSVersion}\n" +
            $"WindowsAppSDK: {typeof(Microsoft.Windows.Storage.Pickers.FileOpenPicker).Assembly.GetName().Version}\n" +
            $"WindowIdValid: {windowIdValid}\n" +
            $"UiThread: {uiThread}\n" +
            $"TrackingId: {trackingId}\n" +
            $"Utc: {DateTimeOffset.UtcNow:o}\n" +
            $"Local: {DateTimeOffset.Now:o}\n" +
            $"Stack: {stack}";

        CrashCaptureService.WriteCrash($"picker-{feature}", null, detail);
        return PickerResult<T>.Failed(
            $"선택 창을 열지 못했습니다. 오류 코드: 0x{hResult:X8} · 추적 ID: {trackingId}",
            hResult,
            trackingId);
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPackaged()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id.Name;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RedactPaths(string value)
    {
        var limited = string.Join(
            Environment.NewLine,
            value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Take(8));
        return WindowsPathRegex().Replace(limited, "<path-redacted>");
    }

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n:]+")]
    private static partial Regex WindowsPathRegex();
}
