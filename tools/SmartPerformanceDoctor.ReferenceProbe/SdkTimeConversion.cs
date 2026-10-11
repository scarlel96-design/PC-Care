using Microsoft.Windows.EventTracing;

internal static class SdkTimeConversion
{
    // IsPartial restricts raw Value access. Converted timestamps remain available.
    internal static bool TryTimestamp(TraceTimestamp value, DateTimeOffset minimum, DateTimeOffset maximum, out DateTimeOffset converted)
    {
        converted = default;
        if (!value.HasValue) return false;
        try
        {
            converted = value.DateTimeOffset;
            return converted >= minimum && converted <= maximum;
        }
        catch (Exception) { converted = default; return false; }
    }

    internal static bool TrySeconds(TraceDuration value, out double seconds)
    {
        seconds = 0;
        if (!value.HasValue) return false;
        try
        {
            seconds = (double)value.TotalSeconds;
            return double.IsFinite(seconds) && seconds is > 0 and <= 1;
        }
        catch (Exception) { seconds = 0; return false; }
    }

    internal static bool VerifyContract()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var partial = new TestContext(start, true, false);
        var full = new TestContext(start, false, false);
        var rounded = new TraceTimestamp(partial, new TraceTimestampValue(1_000_000_000));
        var duration = new TraceDuration(partial, new TraceDurationValue(1_000_000));
        var checks = new[] {
            rounded.IsPartial && TryTimestamp(rounded, start, start.AddSeconds(2), out var time) && time == start.AddSeconds(1),
            duration.IsPartial && TrySeconds(duration, out var seconds) && seconds == .001,
            TryTimestamp(new TraceTimestamp(full, new TraceTimestampValue(1_000_000_000)), start, start.AddSeconds(2), out _),
            !TryTimestamp(default, start, start.AddSeconds(2), out _),
            !TrySeconds(default, out _),
            !TryTimestamp(rounded, start, start.AddMilliseconds(500), out _),
            !TryTimestamp(new TraceTimestamp(new TestContext(start, true, true), new TraceTimestampValue(1)), start, start.AddSeconds(2), out _),
            !TrySeconds(new TraceDuration(partial, new TraceDurationValue(-1)), out _),
            !TrySeconds(new TraceDuration(partial, new TraceDurationValue(0)), out _),
            !TrySeconds(new TraceDuration(partial, new TraceDurationValue(2_000_000_000)), out _)
        };
        Console.WriteLine($"SDK_TIME_CONTRACT passed={checks.Count(c => c)} failed={checks.Count(c => !c)} roundedConvertedValues=Accepted rawValue=Unused");
        return checks.All(c => c);
    }

    // The fixture uses SDK public structs. It does not read or generate a trace.
    private sealed class TestContext(DateTimeOffset start, bool partial, bool error) : ITraceTimestampContext
    {
        public TraceClockType ClockType => default;
        public TraceTimestampValue ReferenceValue => new(0);
        public DateTimeOffset ReferenceTime => start;
        public bool IsPartial => partial;
        public DateTimeOffset GetDateTimeOffset(TraceTimestampValue value) => error ? throw new InvalidOperationException("SyntheticConversionError") : start.AddTicks(value.RawValue / 100);
        public Timestamp GetRelativeTimestamp(TraceTimestampValue value) => Timestamp.FromNanoseconds(value.RawValue);
        public TraceTimestampValue GetApproximateValue(Timestamp value) => new(value.Nanoseconds);
    }
}
