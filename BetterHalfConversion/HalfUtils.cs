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