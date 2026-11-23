using System.Runtime.CompilerServices;
namespace PcCare.BenchmarkSamples;
public static class Calculator
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Double(int value) => value * 2;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Evaluate(int value) => Double(value) + 1;
}
