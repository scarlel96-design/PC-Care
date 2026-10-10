using Windows.Win32;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.ProcessStatus;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed class SystemSnapshotProbe
{
    private ulong? _idle, _total;
    public unsafe (double? Cpu, ulong? TotalMemory, ulong? AvailableMemory, ulong? Commit, ulong? CommitLimit) Read()
    {
        double? cpu = null;
        if (PInvoke.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            static ulong T(System.Runtime.InteropServices.ComTypes.FILETIME t) => ((ulong)(uint)t.dwHighDateTime << 32) | (uint)t.dwLowDateTime;
            var total = T(kernel) + T(user);
            if (_total.HasValue && total > _total && T(idle) >= _idle)
                cpu = 100d * (1 - (double)(T(idle) - _idle!.Value) / (total - _total.Value));
            _total = total; _idle = T(idle);
        }
        var memory = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        var memoryOk = PInvoke.GlobalMemoryStatusEx(ref memory);
        var perf = new PERFORMANCE_INFORMATION { cb = (uint)sizeof(PERFORMANCE_INFORMATION) };
        var commitOk = PInvoke.GetPerformanceInfo(ref perf, (uint)sizeof(PERFORMANCE_INFORMATION));
        return (cpu, memoryOk ? memory.ullTotalPhys : null, memoryOk ? memory.ullAvailPhys : null,
            commitOk ? (ulong)perf.CommitTotal * (ulong)perf.PageSize : null,
            commitOk ? (ulong)perf.CommitLimit * (ulong)perf.PageSize : null);
    }
}
