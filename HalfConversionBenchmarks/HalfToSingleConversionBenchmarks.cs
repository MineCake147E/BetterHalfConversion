using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using BetterHalfToSingleConversion;

namespace HalfConversionBenchmarks
{
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams, BenchmarkLogicalGroupRule.ByCategory)]
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    [AnyCategoriesFilter(CategoryStandard)]
    public class HalfToSingleConversionBenchmarks
    {
        private const string CategoryAvx2 = "Avx2";
        private const string CategoryNew = "New";
        private const string CategoryNew2 = "New2";
        private const string CategoryNew3 = "New3";
        private const string CategorySimple = "Simple";
        private const string CategoryStandard = "Standard";
        private const string CategoryUnrolled = "Unrolled";
        private const string CategoryVectorized = "Vectorized";

        private Half[] bufferA;
        private float[] bufferDst;

        [Params(65536)]
        public int Frames { get; set; }
        [Params(InputValueType.Sequential, InputValueType.Permuted)]
        public InputValueType InputValue { get; set; }
        [GlobalSetup]
        public void Setup()
        {
            var samples = Frames;
            bufferDst = new float[samples];
            var bA = bufferA = new Half[samples];
            var spanA = bA.AsSpan();
            switch (InputValue)
            {
                case InputValueType.Permuted:
                    FillSequential(spanA);
                    ref var x9 = ref MemoryMarshal.GetReference(spanA);
                    var length = spanA.Length;
                    var olen = length - 2;
                    for (var i = 0; i < olen; i++)
                    {
                        //Using RandomNumberGenerator in order to prevent predictability
                        var x = RandomNumberGenerator.GetInt32(i, length);
                        (Unsafe.Add(ref x9, x), Unsafe.Add(ref x9, i)) = (Unsafe.Add(ref x9, i), Unsafe.Add(ref x9, x));
                    }
                    break;
                case InputValueType.RandomUniform:
                    RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(spanA));
                    break;
                case InputValueType.RandomSubnormal:
                    for (var i = 0; i < spanA.Length; i++)
                    {
                        var r = (ushort)RandomNumberGenerator.GetInt32(0x7fe);
                        spanA[i] = BitConverter.UInt16BitsToHalf(ushort.RotateRight(r, 1));
                    }
                    break;
                case InputValueType.RandomNormal:
                    for (var i = 0; i < spanA.Length; i++)
                    {
                        var r = (ushort)RandomNumberGenerator.GetInt32(0xF000);
                        spanA[i] = BitConverter.UInt16BitsToHalf((ushort)(ushort.RotateRight(r, 1) + 0x0400u));
                    }
                    break;
                case InputValueType.RandomInfNaN:
                    RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(spanA));
                    for (var i = 0; i < spanA.Length; i++)
                    {
                        var r = BitConverter.HalfToUInt16Bits(spanA[i]);
                        spanA[i] = BitConverter.UInt16BitsToHalf((ushort)(r | 0x7c00u));
                    }
                    break;
                default:
                    FillSequential(spanA);
                    break;
            }
            static void FillSequential(Span<Half> spanA)
            {
                for (var i = 0; i < spanA.Length; i++)
                {
                    spanA[i] = BitConverter.UInt16BitsToHalf((ushort)i);
                }
            }
        }

        [BenchmarkCategory(CategorySimple, CategoryStandard)]
        [Benchmark(Baseline = true)]
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

        [BenchmarkCategory(CategorySimple, CategoryNew)]
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

        [BenchmarkCategory(CategorySimple, CategoryNew2)]
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

        #region Unrolled

        [BenchmarkCategory(CategoryUnrolled, CategoryStandard)]
        [Benchmark(Baseline = true)]
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

        [BenchmarkCategory(CategoryUnrolled, CategoryNew)]
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

        [BenchmarkCategory(CategoryUnrolled, CategoryNew2)]
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
        #endregion
        [BenchmarkCategory(CategoryVectorized, CategoryAvx2)]
        [Benchmark]
        public void UnrolledLoopVectorizedAvx2() => HalfUtils.ConvertHalfToSingleMany(bufferDst.AsSpan(), bufferA.AsSpan());

    }
}
