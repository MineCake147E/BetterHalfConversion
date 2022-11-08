### Description

Currently the conversion between `Half` and `float` is only implemented in software, leading to performance issues. 
It would be ideal if Issue #62416 could be resolved, but better software fallback is still needed for environments like Sandy Bridge, which does not support hardware conversion. 

### Configuration

```ini
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1706 (21H2)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.300-preview.22204.3
  [Host]     : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
```


### Regression?

No

### Data
I benchmarked the code below.
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace HalfConversionBenchmarks
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class HalfToSingleConversionBenchmarks
    {
        [Params(65535)]
        public int Frames { get; set; }

        private float[] bufferDst;
        private Half[] bufferA;

        [GlobalSetup]
        public void Setup()
        {
            var samples = Frames;
            bufferDst = new float[samples];
            bufferA = new Half[samples];
            bufferA.AsSpan().Fill((Half)1.5f);
        }

        [Benchmark]
        public void SimpleLoop()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (float)Unsafe.Add(ref rsi, i);
            }
        }

        [Benchmark]
        public void UnrolledLoop()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = (float)Unsafe.Add(ref rsi, i + 0);
                Unsafe.Add(ref rdi, i + 1) = (float)Unsafe.Add(ref rsi, i + 1);
                Unsafe.Add(ref rdi, i + 2) = (float)Unsafe.Add(ref rsi, i + 2);
                Unsafe.Add(ref rdi, i + 3) = (float)Unsafe.Add(ref rsi, i + 3);
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (float)Unsafe.Add(ref rsi, i);
            }
        }
    }
}

```

|       Method | Frames |     Mean |   Error |  StdDev | Code Size |
|------------- |------- |---------:|--------:|--------:|----------:|
|   SimpleLoop |  65535 | 223.9 μs | 1.58 μs | 1.40 μs |     314 B |
| UnrolledLoop |  65535 | 205.6 μs | 0.89 μs | 0.74 μs |     432 B |

### Analysis

The [current code](https://github.com/dotnet/runtime/blob/621cd59436cb29cab4b1162409ae0947c4bd780d/src/libraries/System.Private.CoreLib/src/System/Half.cs#L599) looks like a source of inefficiency, using a lot of branches.  
By getting rid of branches and utilizing floating-point tricks for solving subnormal issues, it IS an improvement for CPUs with fast FPUs.  
<details>
<summary> My proposal for new software fallback </summary>

I wrote this code for conversion from `Half` to `float` by converting it to `double` first.  
I've tested this code in test project for all possible 65536 Half values.
```csharp
using System.Runtime.CompilerServices;

namespace BetterHalfToSingleConversion
{
    public static class HalfUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float ConvertHalfToSingle(Half value)
        {
            var h = BitConverter.HalfToInt16Bits(value);
            var v = (uint)(int)h;
            var b = (v & 0x7c00u) == 0x7c00u;
            var hb = (ulong)-(long)Unsafe.As<bool, byte>(ref b);
            v <<= 13;
            v &= 0x8FFF_E000;
            var j = 0x0700000000000000ul + (hb & 0x3F00000000000000ul);
            var d = BitConverter.DoubleToUInt64Bits((double)BitConverter.UInt32BitsToSingle(v));
            d += j;
            return (float)BitConverter.UInt64BitsToDouble(d);
        }
    }
}
```
Test code:
```csharp
using System;

using BetterHalfToSingleConversion;

using NUnit.Framework;

namespace BetterHalfConversionTests
{
    [TestFixture]
    public class BetterHalfToSingleConversionTests
    {

        [Test]
        public void ConvertHalfToSingleConvertsAllValuesCorrectly()
        {
            for (uint i = 0; i <= ushort.MaxValue; i++)
            {
                var h = BitConverter.UInt16BitsToHalf((ushort)i);
                var exp = (float)h;
                var act = HalfUtils.ConvertHalfToSingle(h);
                Assert.AreEqual(exp, act, $"Evaluating {i}th value:");
            }
        }
    }
}
```
And benchmarked with:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using BetterHalfToSingleConversion;

namespace HalfConversionBenchmarks
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class HalfToSingleConversionBenchmarks
    {
        [Params(65535)]
        public int Frames { get; set; }

        private float[] bufferDst;
        private Half[] bufferA;

        [GlobalSetup]
        public void Setup()
        {
            var samples = Frames;
            bufferDst = new float[samples];
            bufferA = new Half[samples];
            bufferA.AsSpan().Fill((Half)1.5f);
        }

        [Benchmark]
        public void SimpleLoopStandard()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (float)Unsafe.Add(ref rsi, i);
            }
        }

        [Benchmark]
        public void UnrolledLoopStandard()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = (float)Unsafe.Add(ref rsi, i + 0);
                Unsafe.Add(ref rdi, i + 1) = (float)Unsafe.Add(ref rsi, i + 1);
                Unsafe.Add(ref rdi, i + 2) = (float)Unsafe.Add(ref rsi, i + 2);
                Unsafe.Add(ref rdi, i + 3) = (float)Unsafe.Add(ref rsi, i + 3);
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (float)Unsafe.Add(ref rsi, i);
            }
        }
        [Benchmark]
        public void SimpleLoopNew()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopNew()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i));
            }
        }

    }
}

```
And result is:
``` ini

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1706 (21H2)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.300-preview.22204.3
  [Host]     : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT


```
|               Method | Frames |     Mean |   Error |  StdDev | Code Size |
|--------------------- |------- |---------:|--------:|--------:|----------:|
|   SimpleLoopStandard |  65535 | 223.1 μs | 3.10 μs | 2.75 μs |     314 B |
| UnrolledLoopStandard |  65535 | 220.5 μs | 1.13 μs | 1.06 μs |     432 B |
|        SimpleLoopNew |  65535 | 156.4 μs | 0.81 μs | 0.76 μs |     211 B |
|      UnrolledLoopNew |  65535 | 141.3 μs | 0.99 μs | 0.93 μs |     686 B |

I also added a [new repository](https://github.com/MineCake147E/BetterHalfConversion) for this kind of improvement with a new vectorized approach, which is ~8x faster than `UnrolledLoopNew` on the same PC.  
```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace BetterHalfToSingleConversion
{
    public static class HalfUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float ConvertHalfToSingle(Half value)
        {
            //Scalar-friendly variant
            var h = BitConverter.HalfToInt16Bits(value);
            var v = (uint)(int)h;
            var b = (v & 0x7c00u) == 0x7c00u;
            var hb = (ulong)-(long)Unsafe.As<bool, byte>(ref b);
            v <<= 13;
            v &= 0x8FFF_E000;
            var j = 0x0700000000000000ul + (hb & 0x3F00000000000000ul);
            var d = BitConverter.DoubleToUInt64Bits((double)BitConverter.UInt32BitsToSingle(v));
            d += j;
            return (float)BitConverter.UInt64BitsToDouble(d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float ConvertHalfToSingle2(Half value)
        {
            //Vector-friendly variant
            var h = BitConverter.HalfToInt16Bits(value);
            var v = (uint)(int)h;
            var e = v & 0x7c00u;
            var c = e == 0u;
            var hc = (uint)-Unsafe.As<bool, byte>(ref c);
            var b = e == 0x7c00u;
            var hb = (uint)-Unsafe.As<bool, byte>(ref b);
            var n = hc & 0x3880_0000u;
            var j = 0x3800_0000u | n;
            v <<= 13;
            j += j & hb;
            var s = v & 0x8000_0000u;
            v &= 0x0FFF_E000;
            v += j;
            var k = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(v) - BitConverter.UInt32BitsToSingle(n));
            return BitConverter.UInt32BitsToSingle(k | s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void ConvertHalfToSingleMany(Span<float> destination, ReadOnlySpan<Half> source)
        {
            if (!Avx2.IsSupported)
            {
                throw new NotSupportedException("Avx2 is not supported in this machine!");
            }
            //Vectorized based on Vector-friendly variant
            ref var rsi = ref MemoryMarshal.GetReference(source);
            ref var rdi = ref MemoryMarshal.GetReference(destination);
            nint i = 0, length = Math.Min(source.Length, destination.Length);
            var ymm15 = Vector256.Create(0x0000_7c00u);
            var ymm14 = Vector256.Create(0x3880_0000u);
            var ymm13 = Vector256.Create(0x3800_0000u);
            var ymm12 = Vector256.Create(0x8000_0000u);
            var ymm11 = Vector256.Create(0x0FFF_E000u);
            var ymm10 = Vector256<uint>.Zero;
            var olen = length - 15;
            for (; i < olen; i += 16)
            {
                ref var r8 = ref Unsafe.Add(ref rdi, i);
                var xmm0 = Unsafe.As<Half, Vector128<short>>(ref Unsafe.Add(ref rsi, i + 0));
                var xmm1 = Unsafe.As<Half, Vector128<short>>(ref Unsafe.Add(ref rsi, i + 8));
                var ymm0 = Avx2.ConvertToVector256Int32(xmm0).AsUInt32();
                var ymm1 = Avx2.ConvertToVector256Int32(xmm1).AsUInt32();
                var ymm2 = Avx2.And(ymm15, ymm0);
                var ymm3 = Avx2.And(ymm15, ymm1);
                var ymm4 = Avx2.CompareEqual(ymm10, ymm2);
                var ymm5 = Avx2.CompareEqual(ymm10, ymm3);
                ymm4 = Avx2.And(ymm14, ymm4);
                ymm5 = Avx2.And(ymm14, ymm5);
                ymm2 = Avx2.CompareEqual(ymm15, ymm2);
                ymm3 = Avx2.CompareEqual(ymm15, ymm3);
                ymm2 = Avx2.And(ymm13, ymm2);
                ymm3 = Avx2.And(ymm13, ymm3);
                var ymm6 = Avx2.Or(ymm13, ymm4);
                var ymm7 = Avx2.Or(ymm13, ymm5);
                ymm0 = Avx2.ShiftLeftLogical(ymm0, 13);
                ymm1 = Avx2.ShiftLeftLogical(ymm1, 13);
                ymm2 = Avx2.Add(ymm2, ymm6);
                ymm3 = Avx2.Add(ymm3, ymm7);
                ymm6 = Avx2.And(ymm12, ymm0);
                ymm7 = Avx2.And(ymm12, ymm1);
                ymm0 = Avx2.And(ymm11, ymm0);
                ymm1 = Avx2.And(ymm11, ymm1);
                ymm0 = Avx2.Add(ymm0, ymm2);
                ymm0 = Avx.Subtract(ymm0.AsSingle(), ymm4.AsSingle()).AsUInt32();
                ymm1 = Avx2.Add(ymm1, ymm3);
                ymm1 = Avx.Subtract(ymm1.AsSingle(), ymm5.AsSingle()).AsUInt32();
                ymm0 = Avx2.Or(ymm0, ymm6);
                ymm1 = Avx2.Or(ymm1, ymm7);
                Unsafe.As<float, Vector256<uint>>(ref Unsafe.Add(ref r8, 0)) = ymm0;
                Unsafe.As<float, Vector256<uint>>(ref Unsafe.Add(ref r8, 8)) = ymm1;

            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = ConvertHalfToSingle(Unsafe.Add(ref rsi, i));
            }
        }
    }
}
```
Benchmarking code:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using BetterHalfToSingleConversion;

namespace HalfConversionBenchmarks
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class HalfToSingleConversionBenchmarks
    {
        [Params(65535)]
        public int Frames { get; set; }

        private float[] bufferDst;
        private Half[] bufferA;

        [GlobalSetup]
        public void Setup()
        {
            var samples = Frames;
            bufferDst = new float[samples];
            bufferA = new Half[samples];
            bufferA.AsSpan().Fill((Half)1.5f);
        }

        [Benchmark]
        public void SimpleLoopStandard()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (float)Unsafe.Add(ref rsi, i);
            }
        }

        [Benchmark]
        public void UnrolledLoopStandard()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = (float)Unsafe.Add(ref rsi, i + 0);
                Unsafe.Add(ref rdi, i + 1) = (float)Unsafe.Add(ref rsi, i + 1);
                Unsafe.Add(ref rdi, i + 2) = (float)Unsafe.Add(ref rsi, i + 2);
                Unsafe.Add(ref rdi, i + 3) = (float)Unsafe.Add(ref rsi, i + 3);
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (float)Unsafe.Add(ref rsi, i);
            }
        }
        [Benchmark]
        public void SimpleLoopNew()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopNew()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertHalfToSingle(Unsafe.Add(ref rsi, i));
            }
        }
        [Benchmark]
        public void SimpleLoopNew2()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertHalfToSingle2(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopNew2()
        {
            var bA = bufferA.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertHalfToSingle2(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertHalfToSingle2(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertHalfToSingle2(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertHalfToSingle2(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertHalfToSingle2(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopVectorizedAvx2() => HalfUtils.ConvertHalfToSingleMany(bufferDst.AsSpan(), bufferA.AsSpan());


    }
}
```
Result:
``` ini

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1706 (21H2)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.300-preview.22204.3
  [Host]     : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT


```
|                     Method | Frames |      Mean |    Error |   StdDev | Code Size |
|--------------------------- |------- |----------:|---------:|---------:|----------:|
|         SimpleLoopStandard |  65535 | 222.82 μs | 1.538 μs | 1.439 μs |     314 B |
|       UnrolledLoopStandard |  65535 | 203.91 μs | 0.939 μs | 0.833 μs |     432 B |
|              SimpleLoopNew |  65535 | 157.87 μs | 1.191 μs | 1.114 μs |     211 B |
|            UnrolledLoopNew |  65535 | 143.14 μs | 1.310 μs | 1.162 μs |     686 B |
|             SimpleLoopNew2 |  65535 | 190.40 μs | 0.891 μs | 0.833 μs |     229 B |
|           UnrolledLoopNew2 |  65535 | 174.45 μs | 1.177 μs | 1.101 μs |     748 B |
| UnrolledLoopVectorizedAvx2 |  65535 |  16.29 μs | 0.316 μs | 0.296 μs |     560 B |

Resolving #62416 should be better for Haswell though.

</details>