using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>Fast RAM snapshot via kernel32 — avoids enumerating every process twice.</summary>
internal static class CareMemoryProbe
{
    public readonly struct Snapshot
    {
        public double UsedPercent { get; init; }

        public long AvailMb { get; init; }

        public long TotalMb { get; init; }

        public string[] TopProcesses { get; init; }
    }

    public static Snapshot Capture(bool includeTopProcesses = true)
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return new Snapshot { TopProcesses = [] };
        }

        var usedPct = status.MemoryLoad;
        var top = includeTopProcesses && usedPct >= 55
            ? GetTopProcesses(3)
            : [];

        return new Snapshot
        {
            UsedPercent = usedPct,
            AvailMb = (long)(status.AvailPhys / 1024 / 1024),
            TotalMb = (long)(status.TotalPhys / 1024 / 1024),
            TopProcesses = top
        };
    }

    private static string[] GetTopProcesses(int count)
    {
        var top = new (string Name, long Ws)[count];
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var ws = process.WorkingSet64;
                if (ws <= 0)
                {
                    continue;
                }

                InsertTop(ref top, process.ProcessName, ws);
            }
            catch
            {
                // Skip protected processes.
            }
            finally
            {
                process.Dispose();
            }
        }

        return top
            .Where(t => t.Ws > 0)
            .OrderByDescending(t => t.Ws)
            .Select(t => $"{t.Name} {t.Ws / 1024 / 1024}MB")
            .ToArray();
    }

    private static void InsertTop(ref (string Name, long Ws)[] top, string name, long ws)
    {
        for (var i = 0; i < top.Length; i++)
        {
            if (ws <= top[i].Ws)
            {
                continue;
            }

            for (var j = top.Length - 1; j > i; j--)
            {
                top[j] = top[j - 1];
            }

            top[i] = (name, ws);
            break;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}