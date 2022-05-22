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
