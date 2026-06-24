namespace SmartPerformanceDoctor.App.Services.Update;

public static class UpdateVersionComparer
{
    public static bool TryParse(string? version, out Version value)
    {
        value = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var normalized = version.Trim().TrimStart('v', 'V');
        if (Version.TryParse(normalized, out var parsed))
        {
            value = parsed;
            return true;
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var numbers = new int[Math.Min(4, parts.Length)];
        for (var i = 0; i < numbers.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]))
            {
                return false;
            }
        }

        value = numbers.Length switch
        {
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
        };
        return true;
    }

    public static int Compare(string? left, string? right)
    {
        if (!TryParse(left, out var a))
        {
            a = new Version(0, 0);
        }

        if (!TryParse(right, out var b))
        {
            b = new Version(0, 0);
        }

        return a.CompareTo(b);
    }

    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    public static bool IsSupported(string current, string minimumSupported) =>
        Compare(current, minimumSupported) >= 0;
}