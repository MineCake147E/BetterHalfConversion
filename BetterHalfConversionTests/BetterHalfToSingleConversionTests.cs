using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

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
        public void ConvertSingleToHalfConvertsAllValuesCorrectly()
        {
            TestAllValues(HalfUtils.ConvertSingleToHalf);
        }
        [Test]
        public void ConvertSingleToHalf2ConvertsAllValuesCorrectly()
        {
            TestAllValues(HalfUtils.ConvertSingleToHalf2);
        }
        [Test]
        public void ConvertSingleToHalf3ConvertsAllValuesCorrectly()
        {
            TestAllValues(HalfUtils.ConvertSingleToHalf3);
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