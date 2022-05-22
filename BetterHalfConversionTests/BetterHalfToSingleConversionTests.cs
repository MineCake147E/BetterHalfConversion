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
    }
}