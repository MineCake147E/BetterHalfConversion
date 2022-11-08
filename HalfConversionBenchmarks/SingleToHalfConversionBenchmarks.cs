using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using BetterHalfToSingleConversion;

namespace HalfConversionBenchmarks
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class SingleToHalfConversionBenchmarks
    {
        [Params(65535)]
        public int Frames { get; set; }

        private float[] bufferSrc;
        private Half[] bufferDst;

        [GlobalSetup]
        public void Setup()
        {
            var samples = Frames;
            bufferSrc = new float[samples];
            bufferDst = new Half[samples];
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(bufferSrc.AsSpan()));
        }
        [Benchmark]
        public void SimpleLoopStandard()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (Half)Unsafe.Add(ref rsi, i);
            }
        }

        [Benchmark]
        public void SimpleLoopNew()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void SimpleLoopNew2()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf2(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void SimpleLoopNew3()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf3(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopStandard()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = (Half)Unsafe.Add(ref rsi, i + 0);
                Unsafe.Add(ref rdi, i + 1) = (Half)Unsafe.Add(ref rsi, i + 1);
                Unsafe.Add(ref rdi, i + 2) = (Half)Unsafe.Add(ref rsi, i + 2);
                Unsafe.Add(ref rdi, i + 3) = (Half)Unsafe.Add(ref rsi, i + 3);
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = (Half)Unsafe.Add(ref rsi, i);
            }
        }

        [Benchmark]
        public void UnrolledLoopNew()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertSingleToHalf(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertSingleToHalf(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertSingleToHalf(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertSingleToHalf(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopNew2()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertSingleToHalf2(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertSingleToHalf2(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertSingleToHalf2(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertSingleToHalf2(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf2(Unsafe.Add(ref rsi, i));
            }
        }

        [Benchmark]
        public void UnrolledLoopNew3()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertSingleToHalf3(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertSingleToHalf3(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertSingleToHalf3(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertSingleToHalf3(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf3(Unsafe.Add(ref rsi, i));
            }
        }
    }
}
