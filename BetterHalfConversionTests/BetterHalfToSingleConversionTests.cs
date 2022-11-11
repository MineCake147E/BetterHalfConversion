using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;
using System.Threading.Tasks;

using BetterHalfToSingleConversion;

using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;

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
        [Test]
        public void ConvertHalfToSingle2ConvertsAllValuesCorrectly()
        {
            for (uint i = 0; i <= ushort.MaxValue; i++)
            {
                var h = BitConverter.UInt16BitsToHalf((ushort)i);
                var exp = (float)h;
                var act = HalfUtils.ConvertHalfToSingle2(h);
                Assert.AreEqual(exp, act, $"Evaluating {i}th value:");
            }
        }
        [Test]
        public void ConvertHalfToSingleManyConvertsAllValuesCorrectly()
        {
            var src = new Half[65536];
            var dst = new float[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                src[i] = BitConverter.UInt16BitsToHalf((ushort)i);
            }
            HalfUtils.ConvertHalfToSingleMany(dst, src);
            for (uint i = 0; i < src.Length; i++)
            {
                var h = src[i];
                var exp = (float)h;
                var act = dst[i];
                Assert.AreEqual(exp, act, $"Evaluating {i}th value:");
            }
        }
        [Test]
        public void ConvertSingleToHalfConvertsAllValuesCorrectly() => TestAllValues(HalfUtils.ConvertSingleToHalf);
        [Test]
        public void ConvertSingleToHalf2ConvertsAllValuesCorrectly() => TestAllValues(HalfUtils.ConvertSingleToHalf2);
        [Test]
        public void ConvertSingleToHalf3ConvertsAllValuesCorrectly() => TestAllValues(HalfUtils.ConvertSingleToHalf3);

        [Test]
        public void ConvertSingleToHalfManyAvx2ConvertsAllValuesCorrectly()
        {
            const int MaxLength = 4096;
            const int TestRange = 16777216;
            const uint Threads = (uint)(0x1_0000_0000L / TestRange);

            var fails = new ConcurrentBag<(uint, ushort)>();
            var result = Parallel.For(0, Threads, (a, state) =>
            {
                try
                {
                    var i = (int)(a * TestRange);
                    var r = 0;
                    var d = new Half[MaxLength];
                    var s = new float[MaxLength];
                    var span = s.AsSpan();
                    GenerateIndexes(MemoryMarshal.Cast<float, int>(span), i);
                    do
                    {
                        HalfUtils.ConvertSingleToHalfManyAvx2(d, s);
                        for (var j = 0; j < d.Length; j++)
                        {
                            var k = (uint)(i + j);
                            var src = BitConverter.UInt32BitsToSingle(k);
                            var exp = (Half)src;
                            var act = d[j];
                            if (BitConverter.HalfToUInt16Bits(exp) != BitConverter.HalfToUInt16Bits(act))
                            {
                                fails.Add((k, BitConverter.HalfToUInt16Bits(act)));
                                state.Stop();
                                Assert.AreEqual(exp, act, $"Evaluating {k}th value({src}):");
                                break;
                            }
                        }
                        i += s.Length;
                        r += s.Length;
                        AddIndexes(MemoryMarshal.Cast<float, int>(span), s.Length);
                    } while (r < TestRange && !state.ShouldExitCurrentIteration);
                }
                catch (Exception e)
                {
                    state.Stop();
                    Console.WriteLine(e.ToString());
                    throw;
                }
            });
            Assert.IsEmpty(fails);
            Assert.IsTrue(result.IsCompleted);
        }

        private static void GenerateIndexes(Span<int> dst, int start)
        {
            for (var i = 0; i < dst.Length; i++)
            {
                dst[i] = i + start;
            }
        }
        private static void AddIndexes(Span<int> dst, int offset)
        {
            if (Vector256.IsHardwareAccelerated)
            {
                AddIndexesVector256(dst, offset);
                return;
            }
            AddIndexesFallback(dst, offset);
        }

        private static void AddIndexesFallback(Span<int> dst, int offset)
        {
            for (var i = 0; i < dst.Length; i++)
            {
                dst[i] = dst[i] + offset;
            }
        }

        private static void AddIndexesVector256(Span<int> dst, int offset)
        {
            nint i = 0, length = dst.Length;
            var v15_8s = Vector256.Create(offset);
            ref var x9 = ref MemoryMarshal.GetReference(dst);
            var olen = length - 8 * Vector256<int>.Count + 1;
            for (; i < olen; i += 8 * Vector256<int>.Count)
            {
                var v0_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 0 * Vector256<int>.Count)));
                var v1_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 1 * Vector256<int>.Count)));
                var v2_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 2 * Vector256<int>.Count)));
                var v3_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 3 * Vector256<int>.Count)));
                v0_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 0 * Vector256<int>.Count));
                v1_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 1 * Vector256<int>.Count));
                v2_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 2 * Vector256<int>.Count));
                v3_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 3 * Vector256<int>.Count));
                v0_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 4 * Vector256<int>.Count)));
                v1_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 5 * Vector256<int>.Count)));
                v2_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 6 * Vector256<int>.Count)));
                v3_8s = Vector256.Add(v15_8s, Vector256.LoadUnsafe(ref Unsafe.Add(ref x9, i + 7 * Vector256<int>.Count)));
                v0_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 4 * Vector256<int>.Count));
                v1_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 5 * Vector256<int>.Count));
                v2_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 6 * Vector256<int>.Count));
                v3_8s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 7 * Vector256<int>.Count));
            }
            olen = length - 4 * Vector128<int>.Count + 1;
            for (; i < olen; i += 4 * Vector128<int>.Count)
            {
                var v0_4s = Vector128.Add(v15_8s.GetLower(), Vector128.LoadUnsafe(ref Unsafe.Add(ref x9, i + 0 * Vector128<int>.Count)));
                var v1_4s = Vector128.Add(v15_8s.GetLower(), Vector128.LoadUnsafe(ref Unsafe.Add(ref x9, i + 1 * Vector128<int>.Count)));
                var v2_4s = Vector128.Add(v15_8s.GetLower(), Vector128.LoadUnsafe(ref Unsafe.Add(ref x9, i + 2 * Vector128<int>.Count)));
                var v3_4s = Vector128.Add(v15_8s.GetLower(), Vector128.LoadUnsafe(ref Unsafe.Add(ref x9, i + 3 * Vector128<int>.Count)));
                v0_4s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 0 * Vector128<int>.Count));
                v1_4s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 1 * Vector128<int>.Count));
                v2_4s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 2 * Vector128<int>.Count));
                v3_4s.StoreUnsafe(ref Unsafe.Add(ref x9, i + 3 * Vector128<int>.Count));
            }
            for (; i < length; i++)
            {
                Unsafe.Add(ref x9, i) += offset;
            }
        }

        private static void TestAllValues(Func<float, Half> func2Test)
        {
            var threads = (uint)Environment.ProcessorCount;
            var d = new ConcurrentBag<(uint, ushort)>();
            var result = Parallel.For(0, threads, (a, state) =>
            {
                try
                {
                    var i = (uint)a;
                    do
                    {
                        var src = BitConverter.UInt32BitsToSingle(i);
                        var exp = (Half)src;
                        var act = func2Test(src);
                        i += threads;
                        if (BitConverter.HalfToUInt16Bits(exp) != BitConverter.HalfToUInt16Bits(act))
                        {
                            d.Add((i, BitConverter.HalfToUInt16Bits(act)));
                            state.Stop();
                            Assert.AreEqual(exp, act, $"Evaluating {i}th value({src}):");
                            break;
                        }
                    } while (i > a && !state.ShouldExitCurrentIteration);
                }
                catch (Exception e)
                {
                    state.Stop();
                    Console.WriteLine(e.ToString());
                    throw;
                }
            });
            Assert.IsEmpty(d);
            Assert.IsTrue(result.IsCompleted);
        }

        [TestCase(0x3300_0000u, 0x3300_0001u)]
        [TestCase(0x3f80_1000u, 0x3f80_1001u)]
        [TestCase(0x7f80_0000u, 0x7f80_0001u)]
        public void ConvertSingleToHalf3ConvertsAllValuesCorrectlyInRange(uint start, uint end)
        {
            var max = end - start;
            var i = 0u;
            do
            {
                var src = BitConverter.UInt32BitsToSingle(i + start);
                var exp = (Half)src;
                var act = HalfUtils.ConvertSingleToHalf3(src);
                i += 1;
                if (BitConverter.HalfToUInt16Bits(exp) != BitConverter.HalfToUInt16Bits(act))
                {
                    Assert.AreEqual(exp, act, $"Evaluating {i}th value({src}):");
                    break;
                }
            } while (i > 0 && i <= max);
        }
    }
}