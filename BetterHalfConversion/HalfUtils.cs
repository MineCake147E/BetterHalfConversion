﻿using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace BetterHalfToSingleConversion
{
    public static class HalfUtils
    {
        private const uint ExponentLowerBound = 0x3880_0000u;
        private const uint ExponentOffset = 0x3800_0000u;
        private const uint FloatSignMask = 0x8000_0000u;
        private const uint FloatExponentMask = 0x7f80_0000u;
        private const uint RoundingExponentOffset = 0x0680_0000u;
        private const float MinimumHalfInfinity = 65520.0f;
        #region HalfToSingle
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

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float ConvertHalfToSingle2(Half value)
        {
            var h = BitConverter.HalfToInt16Bits(value);
            var v = (uint)(int)h;
            var e = v & 0x7c00u;
            var c = e == 0u;
            var hc = (uint)-Unsafe.As<bool, byte>(ref c);
            var b = e == 0x7c00u;
            var hb = (uint)-Unsafe.As<bool, byte>(ref b);
            var n = hc & ExponentLowerBound;
            var j = ExponentOffset | n;
            v <<= 13;
            j += j & hb;
            var s = v & FloatSignMask;
            v &= 0x0FFF_E000;
            v += j;
            var k = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(v) - BitConverter.UInt32BitsToSingle(n));
            return BitConverter.UInt32BitsToSingle(k | s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float ConvertHalfToSingle3(Half value)
        {
            var h = BitConverter.HalfToInt16Bits(value);
            var v = (uint)(int)h;
            var e = v & 0x7c00u;
            var c = e == 0u;
            var hc = (uint)-Unsafe.As<bool, byte>(ref c);
            var b = e == 0x7c00u;
            var hb = (uint)-Unsafe.As<bool, byte>(ref b);
            var n = hc & ExponentLowerBound;
            var j = ExponentOffset | n;
            v <<= 13;
            j += j & hb;
            var s = v & FloatSignMask;
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
            var ymm14 = Vector256.Create(ExponentLowerBound);
            var ymm13 = Vector256.Create(ExponentOffset);
            var ymm12 = Vector256.Create(FloatSignMask);
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
        #endregion
        #region SingleToHalf

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static Half ConvertSingleToHalf(float value)
        {
            var v = BitConverter.SingleToUInt32Bits(value);
            var s = v & FloatSignMask;
            v &= ~FloatSignMask;
            value = Vector128.Min(Vector128.CreateScalarUnsafe(MinimumHalfInfinity), Vector128.CreateScalarUnsafe(v).AsSingle()).GetElement(0);
            var dvalue = (double)value;
            var m = v < ExponentLowerBound;
            var dhm = (ulong)-Unsafe.As<bool, sbyte>(ref m);
            var hm = (uint)dhm;
            var dvm = BitConverter.UInt64BitsToDouble(dhm & 0x3F10_0000_0000_0000u);
            hm &= 0x0080_0000u;
            dvalue += dvm;
            value = (float)dvalue;
            var dv = BitConverter.DoubleToUInt64Bits(dvalue);
            dvm = BitConverter.UInt64BitsToDouble((dv + 0x02a0_0000_0000_0000ul) & 0x7FF0_0000_0000_0000ul);
            dvalue += dvm;
            dvalue -= dvm;
            v = BitConverter.SingleToUInt32Bits((float)dvalue);
            var vm = BitConverter.UInt32BitsToSingle(hm);
            v -= ExponentOffset;
            v = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(v) - vm);
            s >>>= 16;
            /*m = (v & 0xfff) > 0;
            hm = (uint)Unsafe.As<bool, byte>(ref m) << 12;
            hm |= ((1 << 13) & v) >> 1;
            v += hm;*/
            v >>>= 13;
            var c = v > 0x7fffu;
            var hc = (uint)-Unsafe.As<bool, byte>(ref c) & 0x7C00u;
            v &= 0x7fffu;
            var gc = hc;
            gc |= s;
            v &= ~hc;
            v |= gc;
            return BitConverter.UInt16BitsToHalf((ushort)v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static Half ConvertSingleToHalf2(float value)
        {
            var v = BitConverter.SingleToUInt32Bits(value);
            var s = v & FloatSignMask;
            v &= ~FloatSignMask;
            value = Vector128.Min(Vector128.CreateScalarUnsafe(MinimumHalfInfinity), Vector128.CreateScalarUnsafe(v).AsSingle()).GetElement(0);
            var dvalue = (double)value;
            var m = v < ExponentLowerBound;
            var dhm = (ulong)-Unsafe.As<bool, sbyte>(ref m);
            var hm = dhm;
            var dvm = BitConverter.UInt64BitsToDouble(dhm & 0x3F10_0000_0000_0000u);
            dvalue += dvm;
            var dv = BitConverter.DoubleToUInt64Bits(dvalue);
            hm &= 0x0010_0000_0000_0000u;
            dvm = BitConverter.UInt64BitsToDouble((dv + 0x02a0_0000_0000_0000ul) & 0x7FF0_0000_0000_0000ul);
            dvalue += dvm;
            dvalue -= dvm;
            dv = BitConverter.DoubleToUInt64Bits(dvalue);
            dvm = BitConverter.UInt64BitsToDouble(hm);
            dv -= 0x3f00_0000_0000_0000ul;
            dv = BitConverter.DoubleToUInt64Bits(BitConverter.UInt64BitsToDouble(dv) - dvm);
            s >>>= 16;
            dv >>>= 42;
            v = (uint)dv;
            var c = dv > 0x7fffu;
            var hc = (uint)-Unsafe.As<bool, byte>(ref c) & 0x7C00u;
            v &= 0x7fffu;
            var gc = hc;
            gc |= s;
            v &= ~hc;
            v |= gc;
            return BitConverter.UInt16BitsToHalf((ushort)v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static Half ConvertSingleToHalf3(float value)
        {
            var exponentLowerBound = Vector128.CreateScalarUnsafe(ExponentLowerBound);
            var exponentOffset = Vector128.CreateScalarUnsafe(ExponentOffset);
            var floatSignMask = Vector128.CreateScalarUnsafe(FloatSignMask);
            var floatExponentMask = Vector128.CreateScalarUnsafe(FloatExponentMask);
            var roundingExponentOffset = Vector128.CreateScalarUnsafe(RoundingExponentOffset);
            var minValueInfinityInHalf = Vector128.CreateScalarUnsafe(MinimumHalfInfinity);
            var x0 = BitConverter.SingleToUInt32Bits(value);
            var s0 = Vector128.CreateScalarUnsafe(value);
            s0 = Vector128.AndNot(s0.AsUInt32(), floatSignMask).AsSingle();
            var sign = x0 & FloatSignMask;
            s0 = Vector128.Min(minValueInfinityInHalf, s0);
            var s1 = Vector128.Max(exponentLowerBound, s0.AsUInt32());
            var isNaN = Vector128.Equals(s0, s0).AsUInt32();
            s1 = Vector128.BitwiseAnd(s1, floatExponentMask);
            s1 = Vector128.Add(s1, roundingExponentOffset);
            var s2 = Vector128.Subtract(s1, exponentOffset);
            s2 = Vector128.BitwiseAnd(s2, isNaN);
            s0 = Vector128.Add(s0, s1.AsSingle());
            s0 = Vector128.Subtract(s0.AsUInt32(), exponentOffset).AsSingle();
            s0 = Vector128.Subtract(s0, s2.AsSingle());
            x0 = s0.AsUInt32().GetElement(0) >> 13;
            sign >>>= 16;
            var hc = ~isNaN.GetElement(0) & 0x7C00u;
            x0 &= 0x7fffu;
            var gc = hc;
            gc |= sign;
            x0 &= ~hc;
            x0 |= gc;
            return BitConverter.UInt16BitsToHalf((ushort)x0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half ConvertSingleToHalf4(float value)
        {
            var v0 = Vector128.CreateScalarUnsafe(0x3880_0000u); //Minimum exponent for rounding
            var v1 = Vector128.CreateScalarUnsafe(0x3800_0000u); //Exponent displacement #1
            var v2 = Vector128.CreateScalarUnsafe(0x8000_0000u); //Sign bit
            var v3 = Vector128.CreateScalarUnsafe(0x7f80_0000u); //Exponent mask
            var v4 = Vector128.CreateScalarUnsafe(0x0680_0000u); //Exponent displacement #2
            var v5 = Vector128.CreateScalarUnsafe(65520.0f);     //Maximum value that is not Infinity in Half
            var v = BitConverter.SingleToUInt32Bits(value);
            var vval = Vector128.CreateScalarUnsafe(value);
            vval = (vval.AsUInt32() & ~v2).AsSingle();  //Clear sign bit
            var s = v & 0x8000_0000u;       //Extract sign bit
            vval = Vector128.Min(v5, vval); //Rectify values that are Infinity in Half
            var w = Vector128.Equals(vval, vval).AsUInt32();   //Detecting NaN(a != a if a is NaN)
            var y = Vector128.Max(v0, vval.AsUInt32()); //Rectify lower exponent
            y &= v3;        //Extract exponent
            y += v4;        //Add exponent by 13
            var z = y - v1; //Subtract exponent from y by 112
            z &= w;         //Zero whole z if value is NaN
            vval += y.AsSingle();                       //Round Single into Half's precision(NaN also gets modified here, just setting the MSB of fraction)
            vval = (vval.AsUInt32() - v1).AsSingle();   //Subtract exponent by 112
            vval -= z.AsSingle();                       //Clear Extra leading 1 set in rounding
            v = vval.AsUInt32().GetElement(0) >> 13;    //Now internal representation is the absolute value represented in Half, shifted 13 bits left, with some exceptions like NaN having strange exponents
            s >>>= 16;                              //Match the position of sign bit
            var hc = ~w.GetElement(0) & 0x7C00u;    //Only exponent bits will be modified if NaN
            v &= 0x7fffu;       //Clear the upper unnecessary bits
            var gc = hc | s;    //Merge sign bit with possible NaN exponent
            v &= ~hc;           //Clear exponents if value is NaN
            v |= gc;            //Merge sign bit and possible NaN exponent
            return BitConverter.UInt16BitsToHalf((ushort)v);    //The final result
        }

        public static Half ConvertSingleToHalf4NoMethodImpl(float value)
        {
            var x0 = BitConverter.SingleToUInt32Bits(value);
            var s0 = Vector128.CreateScalarUnsafe(value);
            s0 = Vector128.AndNot(s0.AsUInt32(), Vector128.CreateScalarUnsafe(FloatSignMask)).AsSingle();
            var sign = x0 & FloatSignMask;
            s0 = Vector128.Min(Vector128.CreateScalarUnsafe(MinimumHalfInfinity), s0);
            var s1 = Vector128.Max(Vector128.CreateScalarUnsafe(ExponentLowerBound), s0.AsUInt32());
            var isNaN = Vector128.Equals(s0, s0).AsUInt32();
            s1 = Vector128.BitwiseAnd(s1, Vector128.CreateScalarUnsafe(FloatExponentMask));
            s1 = Vector128.Add(s1, Vector128.CreateScalarUnsafe(RoundingExponentOffset));
            var v1 = Vector128.CreateScalarUnsafe(ExponentOffset);
            var s2 = Vector128.Subtract(s1, v1);
            s2 = Vector128.BitwiseAnd(s2, isNaN);
            s0 = Vector128.Add(s0, s1.AsSingle());
            s0 = Vector128.Subtract(s0.AsUInt32(), v1).AsSingle();
            s0 = Vector128.Subtract(s0, s2.AsSingle());
            x0 = s0.AsUInt32().GetElement(0) >> 13;
            sign >>>= 16;
            var hc = ~isNaN.GetElement(0) & 0x7C00u;
            x0 &= 0x7fffu;
            var gc = hc;
            gc |= sign;
            x0 &= ~hc;
            x0 |= gc;
            return BitConverter.UInt16BitsToHalf((ushort)x0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Half ConvertSingleToHalf4NoInlining(float value)
        {
            var x0 = BitConverter.SingleToUInt32Bits(value);
            var s0 = Vector128.CreateScalarUnsafe(value);
            s0 = Vector128.AndNot(s0.AsUInt32(), Vector128.CreateScalarUnsafe(FloatSignMask)).AsSingle();
            var sign = x0 & FloatSignMask;
            s0 = Vector128.Min(Vector128.CreateScalarUnsafe(MinimumHalfInfinity), s0);
            var s1 = Vector128.Max(Vector128.CreateScalarUnsafe(ExponentLowerBound), s0.AsUInt32());
            var isNaN = Vector128.Equals(s0, s0).AsUInt32();
            s1 = Vector128.BitwiseAnd(s1, Vector128.CreateScalarUnsafe(FloatExponentMask));
            s1 = Vector128.Add(s1, Vector128.CreateScalarUnsafe(RoundingExponentOffset));
            var v1 = Vector128.CreateScalarUnsafe(ExponentOffset);
            var s2 = Vector128.Subtract(s1, v1);
            s2 = Vector128.BitwiseAnd(s2, isNaN);
            s0 = Vector128.Add(s0, s1.AsSingle());
            s0 = Vector128.Subtract(s0.AsUInt32(), v1).AsSingle();
            s0 = Vector128.Subtract(s0, s2.AsSingle());
            x0 = s0.AsUInt32().GetElement(0) >> 13;
            sign >>>= 16;
            var hc = ~isNaN.GetElement(0) & 0x7C00u;
            x0 &= 0x7fffu;
            var gc = hc;
            gc |= sign;
            x0 &= ~hc;
            x0 |= gc;
            return BitConverter.UInt16BitsToHalf((ushort)x0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void ConvertSingleToHalfManyAvx2(Span<Half> destination, ReadOnlySpan<float> source)
        {
            if (!Avx2.IsSupported)
            {
                throw new NotSupportedException("Avx2 is not supported in this machine!");
            }
            //Vectorized based on Vector-friendly variant
            ref var rsi = ref MemoryMarshal.GetReference(source);
            ref var rdi = ref MemoryMarshal.GetReference(destination);
            nint i = 0, length = Math.Min(source.Length, destination.Length);
            var ymm15 = Vector256.Create(ExponentLowerBound);
            var ymm14 = Vector256.Create(ExponentOffset);
            var ymm13 = Vector256.Create(FloatSignMask);
            var ymm12 = Vector256.Create(FloatExponentMask);
            var ymm11 = Vector256.Create(RoundingExponentOffset);
            var ymm10 = Vector256.Create(MinimumHalfInfinity);
            var x9 = BitConverter.HalfToUInt16Bits(Half.PositiveInfinity);
            var x10 = 0x7fffu;
            var olen = length - 15;
            for (; i < olen; i += 16)
            {
                ref var r8 = ref Unsafe.Add(ref rdi, i);
                var ymm0 = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref rsi, i + 0));
                var ymm1 = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref rsi, i + 8));
                var ymm2 = Avx2.And(ymm0.AsUInt32(), ymm13);
                var ymm3 = Avx2.And(ymm1.AsUInt32(), ymm13);
                ymm0 = Avx2.AndNot(ymm13, ymm0.AsUInt32()).AsSingle();
                ymm1 = Avx2.AndNot(ymm13, ymm1.AsUInt32()).AsSingle();
                ymm0 = Avx.Min(ymm10, ymm0);
                ymm1 = Avx.Min(ymm10, ymm1);
                var ymm4 = Avx2.Max(ymm0.AsUInt32(), ymm15);
                var ymm5 = Avx2.Max(ymm1.AsUInt32(), ymm15);
                ymm4 = Avx2.And(ymm4, ymm12);
                ymm5 = Avx2.And(ymm5, ymm12);
                var ymm8 = Avx.Compare(ymm0, ymm0, FloatComparisonMode.UnorderedNonSignaling);
                var ymm9 = Avx.Compare(ymm1, ymm1, FloatComparisonMode.UnorderedNonSignaling);
                ymm4 = Avx2.Add(ymm4, ymm11);
                ymm5 = Avx2.Add(ymm5, ymm11);
                var ymm6 = Avx2.Subtract(ymm4, ymm14);
                var ymm7 = Avx2.Subtract(ymm5, ymm14);
                ymm0 = Avx.Add(ymm0, ymm4.AsSingle());
                ymm1 = Avx.Add(ymm1, ymm5.AsSingle());
                ymm6 = Avx2.AndNot(ymm8.AsUInt32(), ymm6);
                ymm7 = Avx2.AndNot(ymm9.AsUInt32(), ymm7);
                ymm8 = Avx2.PackSignedSaturate(ymm8.AsInt32(), ymm9.AsInt32()).AsSingle();
                ymm4 = Vector256.Create(x9).AsUInt32();
                ymm5 = Vector256.Create(x10).AsUInt32();
                ymm0 = Avx2.Subtract(ymm0.AsUInt32(), ymm14).AsSingle();
                ymm1 = Avx2.Subtract(ymm1.AsUInt32(), ymm14).AsSingle();
                ymm2 = Avx2.PackSignedSaturate(ymm2.AsInt32(), ymm3.AsInt32()).AsUInt32();
                ymm0 = Avx.Subtract(ymm0, ymm6.AsSingle());
                ymm1 = Avx.Subtract(ymm1, ymm7.AsSingle());
                ymm8 = Avx2.And(ymm4, ymm8.AsUInt32()).AsSingle();
                ymm0 = Avx2.ShiftRightLogical(ymm0.AsUInt32(), 13).AsSingle();
                ymm1 = Avx2.ShiftRightLogical(ymm1.AsUInt32(), 13).AsSingle();
                ymm0 = Avx2.And(ymm0.AsUInt32(), ymm5).AsSingle();
                ymm1 = Avx2.And(ymm1.AsUInt32(), ymm5).AsSingle();
                ymm0 = Avx2.PackSignedSaturate(ymm0.AsInt32(), ymm1.AsInt32()).AsSingle();
                ymm2 = Avx2.Or(ymm2, ymm8.AsUInt32());
                ymm0 = Avx2.AndNot(ymm8.AsUInt32(), ymm0.AsUInt32()).AsSingle();
                ymm2 = Avx2.Or(ymm0.AsUInt32(), ymm2);
                ymm2 = Avx2.Permute4x64(ymm2.AsDouble(), 0b11_01_10_00).AsUInt32();
                Unsafe.As<Half, Vector256<uint>>(ref Unsafe.Add(ref r8, 0)) = ymm2;
            }
            for (; i < length; i++)
            {
                var value = Unsafe.Add(ref rsi, i);
                var v = BitConverter.SingleToUInt32Bits(value);
                var vval = Vector128.CreateScalarUnsafe(value);
                vval = Vector128.AndNot(vval, ymm13.GetLower().AsSingle());
                var s = v & FloatSignMask;
                vval = Vector128.Min(ymm10.GetLower(), vval);
                var w = Vector128.Equals(vval, vval);
                var y = Vector128.Max(ymm15.GetLower(), vval.AsUInt32());
                y = Vector128.BitwiseAnd(y, ymm12.GetLower());
                y = Vector128.Add(y, ymm11.GetLower());
                var z = Vector128.Subtract(y, ymm14.GetLower());
                z = Vector128.BitwiseAnd(z, w.AsUInt32());
                vval = Vector128.Add(vval, y.AsSingle());
                vval = Vector128.Subtract(vval.AsUInt32(), ymm14.GetLower()).AsSingle();
                vval = Vector128.Subtract(vval, z.AsSingle());
                v = vval.AsUInt32().GetElement(0) >> 13;
                s >>>= 16;
                var c = v > 0x7fffu;
                var hc = (uint)-Unsafe.As<bool, byte>(ref c) & 0x7C00u;
                v &= 0x7fffu;
                var gc = hc;
                gc |= s;
                v &= ~hc;
                v |= gc;
                Unsafe.Add(ref rdi, i) = BitConverter.UInt16BitsToHalf((ushort)v);
            }
        }
        #endregion
    }
}