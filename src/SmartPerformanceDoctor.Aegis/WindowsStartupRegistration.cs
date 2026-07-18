using System.IO;
using Microsoft.Win32;

namespace SmartPerformanceDoctor.Aegis;

/// <summary>HKCU Run — Windows 로그인 시 PCCare 자동 실행.</summary>
public static class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValueName = "PCCare";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(RunValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null)
            {
                return;
            }

            if (!enabled)
            {
                key.DeleteValue(RunValueName, false);
                return;
            }

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            var quoted = $"\"{executablePath}\" --background";
            key.SetValue(RunValueName, quoted, RegistryValueKind.String);
        }
        catch
        {
            // best effort
        }
    }

    public static void Remove()
    {
        SetEnabled(false, "");
    }
}