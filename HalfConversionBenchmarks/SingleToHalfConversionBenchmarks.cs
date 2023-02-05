using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using BetterHalfToSingleConversion;

namespace HalfConversionBenchmarks
{
    [CategoriesColumn]
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams, BenchmarkLogicalGroupRule.ByCategory)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    [AnyCategoriesFilter(CategoryNew4, CategoryNew3)]
    public class SingleToHalfConversionBenchmarks
    {
        [Params(65536)]
        public int Frames { get; set; }

        [Params(InputValueType.Permuted)]
        public InputValueType InputValue { get; set; }

        private const string CategorySimple = "Simple";
        private const string CategoryUnrolled = "Unrolled";
        private const string CategoryVectorized = "Vectorized";
        private const string CategoryStandard = "Standard";
        private const string CategoryNew = "New";
        private const string CategoryNew2 = "New2";
        private const string CategoryNew3 = "New3";
        private const string CategoryNew4 = "New4";
        private const string CategoryAggressiveInlining = "AggressiveInlining";
        private const string CategoryInliningUnspecified = "InliningUnspecified";
        private const string CategoryNoInlining = "NoInlining";
        private const string CategoryAvx2 = "Avx2";

        private float[] bufferSrc;
        private Half[] bufferDst;

        [GlobalSetup]
        public void Setup()
        {
            var samples = Frames;
            var vS = bufferSrc = new float[samples];
            bufferDst = new Half[samples];
            var vspan = vS.AsSpan();
            switch (InputValue)
            {
                case InputValueType.Permuted:
                    FillSequential(vspan);
                    //Random Permutation
                    ref var x9 = ref MemoryMarshal.GetReference(vspan);
                    var length = vspan.Length;
                    var olen = length - 2;
                    for (var i = 0; i < olen; i++)
                    {
                        //Using RandomNumberGenerator in order to prevent predictability
                        var x = RandomNumberGenerator.GetInt32(i, length);
                        (Unsafe.Add(ref x9, x), Unsafe.Add(ref x9, i)) = (Unsafe.Add(ref x9, i), Unsafe.Add(ref x9, x));
                    }
                    break;
                case InputValueType.RandomUniform:
                    RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(vspan));
                    break;
                case InputValueType.RandomSubnormal:
                    for (var i = 0; i < vspan.Length; i++)
                    {
                        var r = (uint)RandomNumberGenerator.GetInt32(0x70FF_BFFE);
                        vspan[i] = BitConverter.UInt32BitsToSingle(uint.RotateRight(r, 1));
                    }
                    break;
                case InputValueType.RandomNormal:
                    for (var i = 0; i < vspan.Length; i++)
                    {
                        var r = (uint)RandomNumberGenerator.GetInt32(0x1E00_1FFE);
                        vspan[i] = BitConverter.UInt32BitsToSingle(uint.RotateRight(r, 1) + 947904512u);
                    }
                    break;
                case InputValueType.RandomInfNaN:
                    RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(vspan));
                    for (var i = 0; i < vspan.Length; i++)
                    {
                        var r = BitConverter.SingleToUInt32Bits(vspan[i]);
                        vspan[i] = BitConverter.UInt32BitsToSingle(r | 0x7f80_0000u);
                    }
                    break;
                default:
                    FillSequential(vspan);
                    break;
            }

            static void FillSequential(Span<float> vspan)
            {
                for (var i = 0; i < vspan.Length; i++)
                {
                    vspan[i] = (float)BitConverter.UInt16BitsToHalf((ushort)i);
                }
            }
        }
        [BenchmarkCategory(CategorySimple, CategoryStandard)]
        [Benchmark(Baseline = true)]
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

        [BenchmarkCategory(CategorySimple, CategoryNew)]
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

        [BenchmarkCategory(CategorySimple, CategoryNew2)]
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

        [BenchmarkCategory(CategorySimple, CategoryNew3)]
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

        [BenchmarkCategory(CategorySimple, CategoryNew4, CategoryAggressiveInlining)]
        [Benchmark]
        public void SimpleLoopNew4A()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf4(Unsafe.Add(ref rsi, i));
            }
        }

        [BenchmarkCategory(CategorySimple, CategoryNew4, CategoryInliningUnspecified)]
        [Benchmark]
        public void SimpleLoopNew4U()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf4NoMethodImpl(Unsafe.Add(ref rsi, i));
            }
        }

        [BenchmarkCategory(CategorySimple, CategoryNew4, CategoryNoInlining)]
        [Benchmark]
        public void SimpleLoopNew4N()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf4NoInlining(Unsafe.Add(ref rsi, i));
            }
        }
        #region Unrolled
        [BenchmarkCategory(CategoryUnrolled, CategoryStandard)]
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

        [BenchmarkCategory(CategoryUnrolled, CategoryNew)]
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

        [BenchmarkCategory(CategoryUnrolled, CategoryNew2)]
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

        [BenchmarkCategory(CategoryUnrolled, CategoryNew3)]
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

        [BenchmarkCategory(CategoryUnrolled, CategoryNew4, CategoryAggressiveInlining)]
        [Benchmark]
        public void UnrolledLoopNew4A()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertSingleToHalf4(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertSingleToHalf4(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertSingleToHalf4(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertSingleToHalf4(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf4(Unsafe.Add(ref rsi, i));
            }
        }

        [BenchmarkCategory(CategoryUnrolled, CategoryNew4, CategoryInliningUnspecified)]
        [Benchmark]
        public void UnrolledLoopNew4U()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertSingleToHalf4NoMethodImpl(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertSingleToHalf4NoMethodImpl(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertSingleToHalf4NoMethodImpl(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertSingleToHalf4NoMethodImpl(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf4NoMethodImpl(Unsafe.Add(ref rsi, i));
            }
        }

        [BenchmarkCategory(CategoryUnrolled, CategoryNew4, CategoryNoInlining)]
        [Benchmark]
        public void UnrolledLoopNew4N()
        {
            var bA = bufferSrc.AsSpan();
            var bD = bufferDst.AsSpan();
            ref var rsi = ref MemoryMarshal.GetReference(bA);
            ref var rdi = ref MemoryMarshal.GetReference(bD);
            nint i = 0, length = Math.Min(bA.Length, bD.Length);
            var olen = length - 3;
            for (; i < olen; i += 4)
            {
                Unsafe.Add(ref rdi, i + 0) = HalfUtils.ConvertSingleToHalf4NoInlining(Unsafe.Add(ref rsi, i + 0));
                Unsafe.Add(ref rdi, i + 1) = HalfUtils.ConvertSingleToHalf4NoInlining(Unsafe.Add(ref rsi, i + 1));
                Unsafe.Add(ref rdi, i + 2) = HalfUtils.ConvertSingleToHalf4NoInlining(Unsafe.Add(ref rsi, i + 2));
                Unsafe.Add(ref rdi, i + 3) = HalfUtils.ConvertSingleToHalf4NoInlining(Unsafe.Add(ref rsi, i + 3));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref rdi, i) = HalfUtils.ConvertSingleToHalf4NoInlining(Unsafe.Add(ref rsi, i));
            }
        }
        #endregion
        #region Vectorized
        [BenchmarkCategory(CategoryVectorized, CategoryAvx2)]
        [Benchmark]
        public void VectorizedLoopAvx2()
        {
            if (!Avx2.IsSupported)
            {
                throw new NotSupportedException("Avx2 is not supported!");
            }
            HalfUtils.ConvertSingleToHalfManyAvx2(bufferDst, bufferSrc);
        }
        #endregion
    }
}
