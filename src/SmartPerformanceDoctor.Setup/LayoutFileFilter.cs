using System.IO;

namespace SmartPerformanceDoctor.Setup;

internal static class LayoutFileFilter
{
    public static bool ShouldSkip(string file, string layoutRoot)
    {
        var name = Path.GetFileName(file);
        if (name.Equals("INSTALLER_README.txt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Equals(".installer-payload.ok", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Equals("installer-layout.zip", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.StartsWith("SmartPerformanceDoctor.Setup", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.StartsWith("PCCare_Setup", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.StartsWith("SmartPerformanceDoctor_v", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}