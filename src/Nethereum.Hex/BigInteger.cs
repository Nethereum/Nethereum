//#if DOTNET35
////
//// System.Numerics.BigInteger
////
//// Rodrigo Kumpera (rkumpera@novell.com)

////
//// Copyright (C) 2010 Novell, Inc (http://www.novell.com)
////
//// Permission is hereby granted, free of charge, to any person obtaining
//// a copy of this software and associated documentation files (the
//// "Software"), to deal in the Software without restriction, including
//// without limitation the rights to use, copy, modify, merge, publish,
//// distribute, sublicense, and/or sell copies of the Software, and to
//// permit persons to whom the Software is furnished to do so, subject to
//// the following conditions:
//// 
//// The above copyright notice and this permission notice shall be
//// included in all copies or substantial portions of the Software.
//// 
//// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
////
//// A big chuck of code comes the DLR (as hosted in http://ironpython.codeplex.com), 
//// which has the following License:
////
///* ****************************************************************************
// *
// * Copyright (c) Microsoft Corporation. 
// *
// * This source code is subject to terms and conditions of the Microsoft Public License. A 
// * copy of the license can be found in the License.html file at the root of this distribution. If 
// * you cannot locate the  Microsoft Public License, please send an email to 
// * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
// * by the terms of the Microsoft Public License.
// *
// * You must not remove this notice, or any other, from this software.
// *
// *
// * ***************************************************************************/

//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Diagnostics.CodeAnalysis;
//using System.Globalization;
//using System.Text;
//using System.Threading;

///*
//Optimization
//	Have proper popcount function for IsPowerOfTwo
//	Use unsafe ops to avoid bounds check
//	CoreAdd could avoid some resizes by checking for equal sized array that top overflow
//	For bitwise operators, hoist the conditionals out of their main loop
//	Optimize BitScanBackward
//	Use a carry variable to make shift opts do half the number of array ops.
//	Schoolbook multiply is O(n^2), use Karatsuba /Toom-3 for large numbers
//*/
//namespace System.Numerics {

//	public struct BigInteger : IComparable, IFormattable, IComparable<BigInteger>, IEquatable<BigInteger>
//	{
//		//LSB on [0]
//		readonly uint[] data;
//		readonly short sign;

//		static readonly uint[] ZERO = new uint [1];
//		static readonly uint[] ONE = new uint [1] { 1 };

//		BigInteger (short sign, uint[] data)
//		{
//			this.sign = sign;
//			this.data = data;
//		}

//		public BigInteger (int value)
//		{
//			if (value == 0) {
//				sign = 0;
//				data = ZERO;
//			} else if (value > 0) {
//				sign = 1;
//				data = new uint[] { (uint) value };
//			} else {
//				sign = -1;
//				data = new uint[1] { (uint)-value };
//			}
//		}

//		[CLSCompliantAttribute (false)]
//		public BigInteger (uint value)
//		{
//			if (value == 0) {
//				sign = 0;
//				data = ZERO;
//			} else {
//				sign = 1;
//				data = new uint [1] { value };
//			}
//		}

//		public BigInteger (long value)
//		{
//			if (value == 0) {
//				sign = 0;
//				data = ZERO;
//			} else if (value > 0) {
//				sign = 1;
//				uint low = (uint)value;
//				uint high = (uint)(value >> 32);

//				data = new uint [high != 0 ? 2 : 1];
//				data [0] = low;
//				if (high != 0)
//					data [1] = high;
//			} else {
//				sign = -1;
//				value = -value;
//				uint low = (uint)value;
//				uint high = (uint)((ulong)value >> 32);

//				data = new uint [high != 0 ? 2 : 1];
//				data [0] = low;
//				if (high != 0)
//					data [1] = high;
//			}			
//		}

//		[CLSCompliantAttribute (false)]
//		public BigInteger (ulong value)
//		{
//			if (value == 0) {
//				sign = 0;
//				data = ZERO;
//			} else {
//				sign = 1;
//				uint low = (uint)value;
//				uint high = (uint)(value >> 32);

//				data = new uint [high != 0 ? 2 : 1];
//				data [0] = low;
//				if (high != 0)
//					data [1] = high;
//			}
//		}


//		static bool Negative (byte[] v)
//		{
//			return ((v[7] & 0x80) != 0);
//		}

//		static ushort Exponent (byte[] v)
//		{
//			return (ushort)((((ushort)(v[7] & 0x7F)) << (ushort)4) | (((ushort)(v[6] & 0xF0)) >> 4));
//		}

//		static ulong Mantissa(byte[] v)
//		{
//			uint i1 = ((uint)v[0] | ((uint)v[1] << 8) | ((uint)v[2] << 16) | ((uint)v[3] << 24));
//			uint i2 = ((uint)v[4] | ((uint)v[5] << 8) | ((uint)(v[6] & 0xF) << 16));

//			return (ulong)((ulong)i1 | ((ulong)i2 << 32));
//		}

//		const int bias = 1075;
//		public BigInteger (double value)
//		{
//			if (double.IsNaN (value) || Double.IsInfinity (value))
//				throw new OverflowException ();

//			byte[] bytes = BitConverter.GetBytes (value);
//			ulong mantissa = Mantissa (bytes);
//			if (mantissa == 0) {
//				// 1.0 * 2**exp, we have a power of 2
//				int exponent = Exponent (bytes);
//				if (exponent == 0) {
//					sign = 0;
//					data = ZERO;
//					return;
//				}

//				BigInteger res = Negative (bytes) ? MinusOne : One;
//				res = res << (exponent - 0x3ff);
//				this.sign = res.sign;
//				this.data = res.data;
//			} else {
//				// 1.mantissa * 2**exp
//				int exponent = Exponent(bytes);
//				mantissa |= 0x10000000000000ul;
//				BigInteger res = mantissa;
//				res = exponent > bias ? res << (exponent - bias) : res >> (bias - exponent);

//				this.sign = (short) (Negative (bytes) ? -1 : 1);
//				this.data = res.data;
//			}
//		}

//		public BigInteger (float value) : this ((double)value)
//		{
//		}

//		const Int32 DecimalScaleFactorMask = 0x00FF0000;
//		const Int32 DecimalSignMask = unchecked((Int32)0x80000000);

//		public BigInteger (decimal value)
//		{
//			// First truncate to get scale to 0 and extract bits
//			int[] bits = Decimal.GetBits(Decimal.Truncate(value));

//			int size = 3;
//			while (size > 0 && bits[size - 1] == 0) size--;

//			if (size == 0) {
//				sign = 0;
//				data = ZERO;
//				return;
//			}

//			sign = (short) ((bits [3] & DecimalSignMask) != 0 ? -1 : 1);

//			data = new uint [size];
//			data [0] = (uint)bits [0];
//			if (size > 1)
//				data [1] = (uint)bits [1];
//			if (size > 2)
//				data [2] = (uint)bits [2];
//		}

//		[CLSCompliantAttribute (false)]
//		public BigInteger (byte[] value)
//		{
//			if (value == null)
//				throw new ArgumentNullException ("value");

//			int len = value.Length;

//			if (len == 0 || (len == 1 && value [0] == 0)) {
//				sign = 0;
//				data = ZERO;
//				return;
//			}

//			if ((value [len - 1] & 0x80) != 0)
//				sign = -1;
//			else
//				sign = 1;

//			if (sign == 1) {
//				while (value [len - 1] == 0)
//					--len;

//				int full_words, size;
//				full_words = size = len / 4;
//				if ((len & 0x3) != 0)
//					++size;

//				data = new uint [size];
//				int j = 0;
//				for (int i = 0; i < full_words; ++i) {
//					data [i] =	(uint)value [j++] |
//								(uint)(value [j++] << 8) |
//								(uint)(value [j++] << 16) |
//								(uint)(value [j++] << 24);
//				}
//				size = len & 0x3;
//				if (size > 0) {
//					int idx = data.Length - 1;
//					for (int i = 0; i < size; ++i)
//						data [idx] |= (uint)(value [j++] << (i * 8));
//				}
//			} else {
//				int full_words, size;
//				full_words = size = len / 4;
//				if ((len & 0x3) != 0)
//					++size;

//				data = new uint [size];

//				uint word, borrow = 1;
//				ulong sub = 0;
//				int j = 0;

//				for (int i = 0; i < full_words; ++i) {
//					word =	(uint)value [j++] |
//							(uint)(value [j++] << 8) |
//							(uint)(value [j++] << 16) |
//							(uint)(value [j++] << 24);

//					sub = (ulong)word - borrow;
//					word = (uint)sub;
//					borrow = (uint)(sub >> 32) & 0x1u;
//					data [i] = ~word;
//				}
//				size = len & 0x3;

//				if (size > 0) {
//					word = 0;
//					uint store_mask = 0;
//					for (int i = 0; i < size; ++i) {
//						word |= (uint)(value [j++] << (i * 8));
//						store_mask = (store_mask << 8) | 0xFF;
//					}

//					sub = word - borrow;
//					word = (uint)sub;
//					borrow = (uint)(sub >> 32) & 0x1u;

//					data [data.Length - 1] = ~word & store_mask;
//				}
//				if (borrow != 0) //FIXME I believe this can't happen, can someone write a test for it?
//					throw new Exception ("non zero final carry");
//			}

//		}

//		public bool IsEven {
//			get { return (data [0] & 0x1) == 0; }
//		}		

//		public bool IsOne {
//			get { return sign == 1 && data.Length == 1 && data [0] == 1; }
//		}		


//		//Gem from Hacker's Delight
//		//Returns the number of bits set in @x
//		static int PopulationCount (uint x)
//		{
//			x = x - ((x >> 1) & 0x55555555);
//			x = (x & 0x33333333) + ((x >> 2) & 0x33333333);
//			x = (x + (x >> 4)) & 0x0F0F0F0F;
//			x = x + (x >> 8);
//			x = x + (x >> 16);
//			return (int)(x & 0x0000003F);
//		}

//		public bool IsPowerOfTwo {
//			get {
//				bool foundBit = false;
//				if (sign != 1)
//					return false;
//				//This function is pop count == 1 for positive numbers
//				for (int i = 0; i < data.Length; ++i) {
//					int p = PopulationCount (data [i]);
//					if (p > 0) {
//						if (p > 1 || foundBit)
//							return false;
//						foundBit = true;
//					}
//				}
//				return foundBit;
//			}
//		}		

//		public bool IsZero {
//			get { return sign == 0; }
//		}		

//		public int Sign {
//			get { return sign; }
//		}

//		public static BigInteger MinusOne {
//			get { return new BigInteger (-1, ONE); }
//		}

//		public static BigInteger One {
//			get { return new BigInteger (1, ONE); }
//		}

//		public static BigInteger Zero {
//			get { return new BigInteger (0, ZERO); }
//		}

//		public static explicit operator int (BigInteger value)
//		{
//			if (value.data.Length > 1)
//				throw new OverflowException ();
//			uint data = value.data [0];

//			if (value.sign == 1) {
//				if (data > (uint)int.MaxValue)
//					throw new OverflowException ();
//				return (int)data;
//			} else if (value.sign == -1) {
//				if (data > 0x80000000u)
//					throw new OverflowException ();
//				return -(int)data;
//			}

//			return 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static explicit operator uint (BigInteger value)
//		{
//			if (value.data.Length > 1 || value.sign == -1)
//				throw new OverflowException ();
//			return value.data [0];
//		}

//		public static explicit operator short (BigInteger value)
//		{
//			int val = (int)value;
//			if (val < short.MinValue || val > short.MaxValue)
//				throw new OverflowException ();
//			return (short)val;
//		}

//		[CLSCompliantAttribute (false)]
//		public static explicit operator ushort (BigInteger value)
//		{
//			uint val = (uint)value;
//			if (val > ushort.MaxValue)
//				throw new OverflowException ();
//			return (ushort)val;
//		}

//		public static explicit operator byte (BigInteger value)
//		{
//			uint val = (uint)value;
//			if (val > byte.MaxValue)
//				throw new OverflowException ();
//			return (byte)val;
//		}

//		[CLSCompliantAttribute (false)]
//		public static explicit operator sbyte (BigInteger value)
//		{
//			int val = (int)value;
//			if (val < sbyte.MinValue || val > sbyte.MaxValue)
//				throw new OverflowException ();
//			return (sbyte)val;
//		}


//		public static explicit operator long (BigInteger value)
//		{
//			if (value.sign == 0)
//				return 0;

//			if (value.data.Length > 2)
//				throw new OverflowException ();

//			uint low = value.data [0];

//			if (value.data.Length == 1) {
//				if (value.sign == 1)
//					return (long)low;
//				long res = (long)low;
//				return -res;
//			}

//			uint high = value.data [1];

//			if (value.sign == 1) {
//				if (high >= 0x80000000u)
//					throw new OverflowException ();
//				return (((long)high) << 32) | low;
//			}

//			if (high > 0x80000000u)
//				throw new OverflowException ();

//			return - ((((long)high) << 32) | (long)low);
//		}

//		[CLSCompliantAttribute (false)]
//		public static explicit operator ulong (BigInteger value)
//		{
//			if (value.data.Length > 2 || value.sign == -1)
//				throw new OverflowException ();

//			uint low = value.data [0];
//			if (value.data.Length == 1)
//				return low;

//			uint high = value.data [1];
//			return (((ulong)high) << 32) | low;
//		}

//		public static explicit operator double (BigInteger value)
//		{
//			//FIXME
//			try {
//	            return double.Parse (value.ToString (),
//    	            System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
//			} catch (OverflowException) {
//				return value.sign == -1 ? double.NegativeInfinity : double.PositiveInfinity;
//			}
//        }

//		public static explicit operator float (BigInteger value)
//		{
//			//FIXME
//			try {
//				return float.Parse (value.ToString (),
//				System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
//			} catch (OverflowException) {
//				return value.sign == -1 ? float.NegativeInfinity : float.PositiveInfinity;
//			}
//		}

//		public static explicit operator decimal (BigInteger value)
//		{
//			if (value.sign == 0)
//			return Decimal.Zero;

//			uint[] data = value.data;
//			if (data.Length > 3) 
//				throw new OverflowException ();

//			int lo = 0, mi = 0, hi = 0;
//			if (data.Length > 2)
//				hi = (Int32)data [2];
//			if (data.Length > 1)
//				mi = (Int32)data [1];
//			if (data.Length > 0)
//				lo = (Int32)data [0];

//			return new Decimal(lo, mi, hi, value.sign < 0, 0);
//		}

//		public static implicit operator BigInteger (int value)
//		{
//			return new BigInteger (value);
//		}

//		[CLSCompliantAttribute (false)]
//		public static implicit operator BigInteger (uint value)
//		{
//			return new BigInteger (value);
//		}

//		public static implicit operator BigInteger (short value)
//		{
//			return new BigInteger (value);
//		}

//		[CLSCompliantAttribute (false)]
//		public static implicit operator BigInteger (ushort value)
//		{
//			return new BigInteger (value);
//		}

//		public static implicit operator BigInteger (byte value)
//		{
//			return new BigInteger (value);
//		}

//		[CLSCompliantAttribute (false)]
//		public static implicit operator BigInteger (sbyte value)
//		{
//			return new BigInteger (value);
//		}

//		public static implicit operator BigInteger (long value)
//		{
//			return new BigInteger (value);
//		}

//		[CLSCompliantAttribute (false)]
//		public static implicit operator BigInteger (ulong value)
//		{
//			return new BigInteger (value);
//		}

//		public static explicit operator BigInteger (double value)
//		{
//			return new BigInteger (value);
//		}

//		public static explicit operator BigInteger (float value)
//		{
//			return new BigInteger (value);
//		}

//		public static explicit operator BigInteger (decimal value)
//		{
//			return new BigInteger (value);
//		}

//		public static BigInteger operator+ (BigInteger left, BigInteger right)
//		{
//			if (left.sign == 0)
//				return right;
//			if (right.sign == 0)
//				return left;

//			if (left.sign == right.sign)
//				return new BigInteger (left.sign, CoreAdd (left.data, right.data));

//			int r = CoreCompare (left.data, right.data);

//			if (r == 0)	
//				return new BigInteger (0, ZERO);

//			if (r > 0) //left > right
//				return new BigInteger (left.sign, CoreSub (left.data, right.data));

//			return new BigInteger (right.sign, CoreSub (right.data, left.data));
//		}

//		public static BigInteger operator- (BigInteger left, BigInteger right)
//		{
//			if (right.sign == 0)
//				return left;
//			if (left.sign == 0)
//				return new BigInteger ((short)-right.sign, right.data);

//			if (left.sign == right.sign) {
//				int r = CoreCompare (left.data, right.data);

//				if (r == 0)	
//					return new BigInteger (0, ZERO);

//				if (r > 0) //left > right
//					return new BigInteger (left.sign, CoreSub (left.data, right.data));

//				return new BigInteger ((short)-right.sign, CoreSub (right.data, left.data));
//			}

//			return new BigInteger (left.sign, CoreAdd (left.data, right.data));
//		}

//		public static BigInteger operator* (BigInteger left, BigInteger right)
//		{
//			if (left.sign == 0 || right.sign == 0)
//				return new BigInteger (0, ZERO);

//			if (left.data [0] == 1 && left.data.Length == 1) {
//				if (left.sign == 1)
//					return right;
//				return new BigInteger ((short)-right.sign, right.data);
//			}

//			if (right.data [0] == 1 && right.data.Length == 1) {
//				if (right.sign == 1)
//					return left;
//				return new BigInteger ((short)-left.sign, left.data);
//			}

//			uint[] a = left.data;
//			uint[] b = right.data;

//			uint[] res = new uint [a.Length + b.Length];

//            for (int i = 0; i < a.Length; ++i) {
//                uint ai = a [i];
//                int k = i;

//                ulong carry = 0;
//                for (int j = 0; j < b.Length; ++j) {
//                    carry = carry + ((ulong)ai) * b [j] + res [k];
//                    res[k++] = (uint)carry;
//                    carry >>= 32;
//                }

//                while (carry != 0) {
//                    carry += res [k];
//                    res[k++] = (uint)carry;
//                    carry >>= 32;
//                }
//            }

//			int m;
//			for (m = res.Length - 1; m >= 0 && res [m] == 0; --m) ;
//			if (m < res.Length - 1)
//				res = Resize (res, m + 1);

//			return new BigInteger ((short)(left.sign * right.sign), res);
//		}

//		public static BigInteger operator/ (BigInteger dividend, BigInteger divisor)
//		{
//			if (divisor.sign == 0)
//				throw new DivideByZeroException ();

//			if (dividend.sign == 0) 
//				return dividend;

//			uint[] quotient;
//			uint[] remainder_value;

//			DivModUnsigned (dividend.data, divisor.data, out quotient, out remainder_value);

//			int i;
//			for (i = quotient.Length - 1; i >= 0 && quotient [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);
//			if (i < quotient.Length - 1)
//				quotient = Resize (quotient, i + 1);

//			return new BigInteger ((short)(dividend.sign * divisor.sign), quotient);
//		}

//		public static BigInteger operator% (BigInteger dividend, BigInteger divisor)
//		{
//			if (divisor.sign == 0)
//				throw new DivideByZeroException ();

//			if (dividend.sign == 0)
//				return dividend;

//			uint[] quotient;
//			uint[] remainder_value;

//			DivModUnsigned (dividend.data, divisor.data, out quotient, out remainder_value);

//			int i;
//			for (i = remainder_value.Length - 1; i >= 0 && remainder_value [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);

//			if (i < remainder_value.Length - 1)
//				remainder_value = Resize (remainder_value, i + 1);
//			return new BigInteger (dividend.sign, remainder_value);
//		}

//		public static BigInteger operator- (BigInteger value)
//		{
//			if (value.sign == 0)
//				return value;
//			return new BigInteger ((short)-value.sign, value.data);
//		}

//		public static BigInteger operator+ (BigInteger value)
//		{
//			return value;
//		}

//		public static BigInteger operator++ (BigInteger value)
//		{
//			short sign = value.sign;
//			uint[] data = value.data;
//			if (data.Length == 1) {
//				if (sign == -1 && data [0] == 1)
//					return new BigInteger (0, ZERO);
//				if (sign == 0)
//					return new BigInteger (1, ONE);
//			}

//			if (sign == -1)
//				data = CoreSub (data, 1);
//			else
//				data = CoreAdd (data, 1);

//			return new BigInteger (sign, data);
//		}

//		public static BigInteger operator-- (BigInteger value)
//		{
//			short sign = value.sign;
//			uint[] data = value.data;
//			if (data.Length == 1) {
//				if (sign == 1 && data [0] == 1)
//					return new BigInteger (0, ZERO);
//				if (sign == 0)
//					return new BigInteger (-1, ONE);
//			}

//			if (sign == -1)
//				data = CoreAdd (data, 1);
//			else
//				data = CoreSub (data, 1);

//			return new BigInteger (sign, data);
//		}

//		public static BigInteger operator& (BigInteger left, BigInteger right)
//		{
//			if (left.sign == 0)
//				return left;

//			if (right.sign == 0)
//				return right;

//			uint[] a = left.data;
//			uint[] b = right.data;
//			int ls = left.sign;
//			int rs = right.sign;

//			bool neg_res = (ls == rs) && (ls == -1);

//			uint[] result = new uint [Math.Max (a.Length, b.Length)];

//			ulong ac = 1, bc = 1, borrow = 1;

//			int i;
//			for (i = 0; i < result.Length; ++i) {
//				uint va = 0;
//				if (i < a.Length)
//					va = a [i];
//				if (ls == -1) {
//					ac = ~va + ac;
//					va = (uint)ac;
//					ac = (uint)(ac >> 32);
//				}

//				uint vb = 0;
//				if (i < b.Length)
//					vb = b [i];
//				if (rs == -1) {
//					bc = ~vb + bc;
//					vb = (uint)bc;
//					bc = (uint)(bc >> 32);
//				}

//				uint word = va & vb;

//				if (neg_res) {
//					borrow = word - borrow;
//					word = ~(uint)borrow;
//					borrow = (uint)(borrow >> 32) & 0x1u;
//				}

//				result [i] = word;
//			}

//			for (i = result.Length - 1; i >= 0 && result [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);

//			if (i < result.Length - 1)
//				result = Resize (result, i + 1);

//			return new BigInteger (neg_res ? (short)-1 : (short)1, result);
//		}

//		public static BigInteger operator| (BigInteger left, BigInteger right)
//		{
//			if (left.sign == 0)
//				return right;

//			if (right.sign == 0)
//				return left;

//			uint[] a = left.data;
//			uint[] b = right.data;
//			int ls = left.sign;
//			int rs = right.sign;

//			bool neg_res = (ls == -1) || (rs == -1);

//			uint[] result = new uint [Math.Max (a.Length, b.Length)];

//			ulong ac = 1, bc = 1, borrow = 1;

//			int i;
//			for (i = 0; i < result.Length; ++i) {
//				uint va = 0;
//				if (i < a.Length)
//					va = a [i];
//				if (ls == -1) {
//					ac = ~va + ac;
//					va = (uint)ac;
//					ac = (uint)(ac >> 32);
//				}

//				uint vb = 0;
//				if (i < b.Length)
//					vb = b [i];
//				if (rs == -1) {
//					bc = ~vb + bc;
//					vb = (uint)bc;
//					bc = (uint)(bc >> 32);
//				}

//				uint word = va | vb;

//				if (neg_res) {
//					borrow = word - borrow;
//					word = ~(uint)borrow;
//					borrow = (uint)(borrow >> 32) & 0x1u;
//				}

//				result [i] = word;
//			}

//			for (i = result.Length - 1; i >= 0 && result [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);

//			if (i < result.Length - 1)
//				result = Resize (result, i + 1);

//			return new BigInteger (neg_res ? (short)-1 : (short)1, result);
//		}

//		public static BigInteger operator^ (BigInteger left, BigInteger right)
//		{
//			if (left.sign == 0)
//				return right;

//			if (right.sign == 0)
//				return left;

//			uint[] a = left.data;
//			uint[] b = right.data;
//			int ls = left.sign;
//			int rs = right.sign;

//			bool neg_res = (ls == -1) ^ (rs == -1);

//			uint[] result = new uint [Math.Max (a.Length, b.Length)];

//			ulong ac = 1, bc = 1, borrow = 1;

//			int i;
//			for (i = 0; i < result.Length; ++i) {
//				uint va = 0;
//				if (i < a.Length)
//					va = a [i];
//				if (ls == -1) {
//					ac = ~va + ac;
//					va = (uint)ac;
//					ac = (uint)(ac >> 32);
//				}

//				uint vb = 0;
//				if (i < b.Length)
//					vb = b [i];
//				if (rs == -1) {
//					bc = ~vb + bc;
//					vb = (uint)bc;
//					bc = (uint)(bc >> 32);
//				}

//				uint word = va ^ vb;

//				if (neg_res) {
//					borrow = word - borrow;
//					word = ~(uint)borrow;
//					borrow = (uint)(borrow >> 32) & 0x1u;
//				}

//				result [i] = word;
//			}

//			for (i = result.Length - 1; i >= 0 && result [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);

//			if (i < result.Length - 1)
//				result = Resize (result, i + 1);

//			return new BigInteger (neg_res ? (short)-1 : (short)1, result);
//		}

//		public static BigInteger operator~ (BigInteger value)
//		{
//			if (value.sign == 0)
//				return new BigInteger (-1, ONE);

//			uint[] data = value.data;
//			int sign = value.sign;

//			bool neg_res = sign == 1;

//			uint[] result = new uint [data.Length];

//			ulong carry = 1, borrow = 1;

//			int i;
//			for (i = 0; i < result.Length; ++i) {
//				uint word = data [i];
//				if (sign == -1) {
//					carry = ~word + carry;
//					word = (uint)carry;
//					carry = (uint)(carry >> 32);
//				}

//				word = ~word;

//				if (neg_res) {
//					borrow = word - borrow;
//					word = ~(uint)borrow;
//					borrow = (uint)(borrow >> 32) & 0x1u;
//				}

//				result [i] = word;
//			}

//			for (i = result.Length - 1; i >= 0 && result [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);

//			if (i < result.Length - 1)
//				result = Resize (result, i + 1);

//			return new BigInteger (neg_res ? (short)-1 : (short)1, result);
//		}

//		//returns the 0-based index of the most significant set bit
//		//returns 0 if no bit is set, so extra care when using it
//		static int BitScanBackward (uint word)
//		{
//			for (int i = 31; i >= 0; --i) {
//				uint mask = 1u << i;
//				if ((word & mask) == mask)
//					return i;
//			}
//			return 0;
//		}

//		public static BigInteger operator<< (BigInteger value, int shift)
//		{
//			if (shift == 0 || value.sign == 0)
//				return value;
//			if (shift < 0)
//				return value >> -shift;

//			uint[] data = value.data;
//			int sign = value.sign;

//			int topMostIdx = BitScanBackward (data [data.Length - 1]);
//			int bits = shift - (31 - topMostIdx);
//			int extra_words = (bits >> 5) + ((bits & 0x1F) != 0 ? 1 : 0);

//			uint[] res = new uint [data.Length + extra_words];

//			int idx_shift = shift >> 5;
//			int bit_shift = shift & 0x1F;
//			int carry_shift = 32 - bit_shift;

//			for (int i = 0; i < data.Length; ++i) {
//				uint word = data [i];
//				res [i + idx_shift] |= word << bit_shift;
//				if (i + idx_shift + 1 < res.Length)
//					res [i + idx_shift + 1] = word >> carry_shift;
//			}

//			return new BigInteger ((short)sign, res);
//		}

//		public static BigInteger operator>> (BigInteger value, int shift)
//		{
//			if (shift == 0 || value.sign == 0)
//				return value;
//			if (shift < 0)
//				return value << -shift;

//			uint[] data = value.data;
//			int sign = value.sign;

//			int topMostIdx = BitScanBackward (data [data.Length - 1]);
//			int idx_shift = shift >> 5;
//			int bit_shift = shift & 0x1F;

//			int extra_words = idx_shift;
//			if (bit_shift > topMostIdx)
//				++extra_words;
//			int size = data.Length - extra_words;

//			if (size <= 0) {
//				if (sign == 1)
//					return new BigInteger (0, ZERO);
//				return new BigInteger (-1, ONE);
//			}

//			uint[] res = new uint [size];
//			int carry_shift = 32 - bit_shift;

//			for (int i = data.Length - 1; i >= idx_shift; --i) {
//				uint word = data [i];

//				if (i - idx_shift < res.Length)
//					res [i - idx_shift] |= word >> bit_shift;
//				if (i - idx_shift - 1 >= 0)
//					res [i - idx_shift - 1] = word << carry_shift;
//			}

//			//Round down instead of toward zero
//			if (sign == -1) {
//				for (int i = 0; i < idx_shift; i++) {
//					if (data [i] != 0u) {
//						var tmp = new BigInteger ((short)sign, res);
//						--tmp;
//						return tmp;
//					}
//				}
//				if (bit_shift > 0 && (data [idx_shift] << carry_shift) != 0u) {
//					var tmp = new BigInteger ((short)sign, res);
//					--tmp;
//					return tmp;
//				}
//			}
//			return new BigInteger ((short)sign, res);
//		}

//		public static bool operator< (BigInteger left, BigInteger right)
//		{
//			return Compare (left, right) < 0;
//		}

//		public static bool operator< (BigInteger left, long right)
//		{
//			return left.CompareTo (right) < 0;
//		}


//		public static bool operator< (long left, BigInteger right)
//		{
//			return right.CompareTo (left) > 0;
//		}


//		[CLSCompliantAttribute (false)]
//		public static bool operator< (BigInteger left, ulong right)
//		{
//			return left.CompareTo (right) < 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator< (ulong left, BigInteger right)
//		{
//			return right.CompareTo (left) > 0;
//		}

//		public static bool operator<= (BigInteger left, BigInteger right)
//		{
//			return Compare (left, right) <= 0;
//		}

//		public static bool operator<= (BigInteger left, long right)
//		{
//			return left.CompareTo (right) <= 0;
//		}

//		public static bool operator<= (long left, BigInteger right)
//		{
//			return right.CompareTo (left) >= 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator<= (BigInteger left, ulong right)
//		{
//			return left.CompareTo (right) <= 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator<= (ulong left, BigInteger right)
//		{
//			return right.CompareTo (left) >= 0;
//		}

//		public static bool operator> (BigInteger left, BigInteger right)
//		{
//			return Compare (left, right) > 0;
//		}

//		public static bool operator> (BigInteger left, long right)
//		{
//			return left.CompareTo (right) > 0;
//		}

//		public static bool operator> (long left, BigInteger right)
//		{
//			return right.CompareTo (left) < 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator> (BigInteger left, ulong right)
//		{
//			return left.CompareTo (right) > 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator> (ulong left, BigInteger right)
//		{
//			return right.CompareTo (left) < 0;
//		}

//		public static bool operator>= (BigInteger left, BigInteger right)
//		{
//			return Compare (left, right) >= 0;
//		}

//		public static bool operator>= (BigInteger left, long right)
//		{
//			return left.CompareTo (right) >= 0;
//		}

//		public static bool operator>= (long left, BigInteger right)
//		{
//			return right.CompareTo (left) <= 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator>= (BigInteger left, ulong right)
//		{
//			return left.CompareTo (right) >= 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator>= (ulong left, BigInteger right)
//		{
//			return right.CompareTo (left) <= 0;
//		}

//		public static bool operator== (BigInteger left, BigInteger right)
//		{
//			return Compare (left, right) == 0;
//		}

//		public static bool operator== (BigInteger left, long right)
//		{
//			return left.CompareTo (right) == 0;
//		}

//		public static bool operator== (long left, BigInteger right)
//		{
//			return right.CompareTo (left) == 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator== (BigInteger left, ulong right)
//		{
//			return left.CompareTo (right) == 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator== (ulong left, BigInteger right)
//		{
//			return right.CompareTo (left) == 0;
//		}

//		public static bool operator!= (BigInteger left, BigInteger right)
//		{
//			return Compare (left, right) != 0;
//		}

//		public static bool operator!= (BigInteger left, long right)
//		{
//			return left.CompareTo (right) != 0;
//		}

//		public static bool operator!= (long left, BigInteger right)
//		{
//			return right.CompareTo (left) != 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator!= (BigInteger left, ulong right)
//		{
//			return left.CompareTo (right) != 0;
//		}

//		[CLSCompliantAttribute (false)]
//		public static bool operator!= (ulong left, BigInteger right)
//		{
//			return right.CompareTo (left) != 0;
//		}

//		public override bool Equals (object obj)
//		{
//			if (!(obj is BigInteger))
//				return false;
//			return Equals ((BigInteger)obj);
//		}

//		public bool Equals (BigInteger other)
//		{
//			if (sign != other.sign)
//				return false;
//			if (data.Length != other.data.Length)
//				return false;
//			for (int i = 0; i < data.Length; ++i) {
//				if (data [i] != other.data [i])
//					return false;
//			}
//			return true;
//		}

//		public bool Equals (long other)
//		{
//			return CompareTo (other) == 0;
//		}

//		public override string ToString ()
//		{
//			return ToString (10, null);
//		}

//		string ToStringWithPadding (string format, uint radix, IFormatProvider provider)
//		{
//			if (format.Length > 1) {
//				int precision = Convert.ToInt32(format.Substring (1), CultureInfo.InvariantCulture.NumberFormat);
//				string baseStr = ToString (radix, provider);
//				if (baseStr.Length < precision) {
//					string additional = new String ('0', precision - baseStr.Length);
//					if (baseStr[0] != '-') {
//						return additional + baseStr;
//					} else {
//							return "-" + additional + baseStr.Substring (1);
//					}
//				}
//				return baseStr;
//			}
//			return ToString (radix, provider);
//		}

//		public string ToString (string format)
//		{
//			return ToString (format, null);
//		}

//		public string ToString (IFormatProvider provider)
//		{
//			return ToString (null, provider);
//		}


//		public string ToString (string format, IFormatProvider provider)
//		{
//			if (format == null || format == "")
//				return ToString (10, provider);

//			switch (format[0]) {
//			case 'd':
//			case 'D':
//			case 'g':
//			case 'G':
//			case 'r':
//			case 'R':
//				return ToStringWithPadding (format, 10, provider);
//			case 'x':
//			case 'X':
//				return ToStringWithPadding (format, 16, null);
//			default:
//				throw new FormatException (string.Format ("format '{0}' not implemented", format));
//			}
//		}

//		static uint[] MakeTwoComplement (uint[] v)
//		{
//			uint[] res = new uint [v.Length];

//			ulong carry = 1;
//			for (int i = 0; i < v.Length; ++i) {
//				uint word = v [i];
//				carry = (ulong)~word + carry;
//				word = (uint)carry;
//				carry = (uint)(carry >> 32);
//				res [i] = word;
//			}

//			uint last = res [res.Length - 1];
//			int idx = FirstNonFFByte (last);
//			uint mask = 0xFF;
//			for (int i = 1; i < idx; ++i)
//				mask = (mask << 8) | 0xFF;

//			res [res.Length - 1] = last & mask;
//			return res;
//		}

//		string ToString (uint radix, IFormatProvider provider)
//		{
//			const string characterSet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

//			if (characterSet.Length < radix)
//				throw new ArgumentException ("charSet length less than radix", "characterSet");
//			if (radix == 1)
//				throw new ArgumentException ("There is no such thing as radix one notation", "radix");

//			if (sign == 0)
//				return "0";
//			if (data.Length == 1 && data [0] == 1)
//				return sign == 1 ? "1" : "-1";

//			List<char> digits = new List<char> (1 + data.Length * 3 / 10);

//			BigInteger a;
//			if (sign == 1)
//				a = this;
//			else {
//				uint[] dt = data;
//				if (radix > 10)
//					dt = MakeTwoComplement (dt);
//				a = new BigInteger (1, dt);
//			}		

//			while (a != 0) {
//				BigInteger rem;
//				a = DivRem (a, radix, out rem);
//				digits.Add (characterSet [(int) rem]);
//			}

//			if (sign == -1 && radix == 10) {
//				NumberFormatInfo info = null;
//				if (provider != null)
//					info = provider.GetFormat (typeof (NumberFormatInfo)) as NumberFormatInfo;
//				if (info != null) {
//					string str = info.NegativeSign;
//					for (int i = str.Length - 1; i >= 0; --i)
//						digits.Add (str [i]);
//				} else {
//					digits.Add ('-');
//				}
//			}

//			char last = digits [digits.Count - 1];
//			if (sign == 1 && radix > 10 && (last < '0' || last > '9'))
//				digits.Add ('0');

//			digits.Reverse ();

//			return new String (digits.ToArray ());
//		}

//		public static BigInteger Parse (string value)
//		{
//			Exception ex;
//			BigInteger result;

//			if (!Parse (value, false, out result, out ex))
//				throw ex;
//			return result;
//		}


//		public static bool TryParse (string value, out BigInteger result)
//		{
//			Exception ex;
//			return Parse (value, true, out result, out ex);
//		}

//		static Exception GetFormatException ()
//		{
//			return new FormatException ("Input string was not in the correct format");
//		}

//		static bool ProcessTrailingWhitespace (bool tryParse, string s, int position, ref Exception exc)
//		{
//			int len = s.Length;

//			for (int i = position; i < len; i++){
//				char c = s [i];

//				if (c != 0 && !Char.IsWhiteSpace (c)){
//					if (!tryParse)
//						exc = GetFormatException ();
//					return false;
//				}
//			}
//			return true;
//		}

//		static bool Parse (string s, bool tryParse, out BigInteger result, out Exception exc)
//		{
//			int len;
//			int i, sign = 1;
//			bool digits_seen = false;

//			result = Zero;
//			exc = null;

//			if (s == null) {
//				if (!tryParse)
//					exc = new ArgumentNullException ("value");
//				return false;
//			}

//			len = s.Length;

//			char c;
//			for (i = 0; i < len; i++){
//				c = s [i];
//				if (!Char.IsWhiteSpace (c))
//					break;
//			}

//			if (i == len) {
//				if (!tryParse)
//					exc = GetFormatException ();
//				return false;
//			}

//			var info = Thread.CurrentThread.CurrentCulture.NumberFormat;

//			string negative = info.NegativeSign;
//			string positive = info.PositiveSign;

//			if (string.CompareOrdinal (s, i, positive, 0, positive.Length) == 0)
//				i += positive.Length;
//			else if (string.CompareOrdinal (s, i, negative, 0, negative.Length) == 0) {
//				sign = -1;
//				i += negative.Length;
//			}

//			BigInteger val = Zero;
//			for (; i < len; i++){
//				c = s [i];

//				if (c == '\0') {
//					i = len;
//					continue;
//				}

//				if (c >= '0' && c <= '9'){
//					byte d = (byte) (c - '0');

//					val = val * 10 + d;

//					digits_seen = true;
//				} else if (!ProcessTrailingWhitespace (tryParse, s, i, ref exc))
//					return false;
//			}

//			if (!digits_seen) {
//				if (!tryParse)
//					exc = GetFormatException ();
//				return false;
//			}

//			if (val.sign == 0)
//				result = val;
//			else if (sign == -1)
//				result = new BigInteger (-1, val.data);
//			else
//				result = new BigInteger (1, val.data);

//			return true;
//		}

//		public static BigInteger Min (BigInteger left, BigInteger right)
//		{
//			int ls = left.sign;
//			int rs = right.sign;

//			if (ls < rs)
//				return left;
//			if (rs < ls)
//				return right;

//			int r = CoreCompare (left.data, right.data);
//			if (ls == -1)
//				r = -r;

//			if (r <= 0)
//				return left;
//			return right;
//		}


//		public static BigInteger Max (BigInteger left, BigInteger right)
//		{
//			int ls = left.sign;
//			int rs = right.sign;

//			if (ls > rs)
//				return left;
//			if (rs > ls)
//				return right;

//			int r = CoreCompare (left.data, right.data);
//			if (ls == -1)
//				r = -r;

//			if (r >= 0)
//				return left;
//			return right;
//		}

//		public static BigInteger Abs (BigInteger value)
//		{
//			return new BigInteger ((short)Math.Abs (value.sign), value.data);
//		}


//		public static BigInteger DivRem (BigInteger dividend, BigInteger divisor, out BigInteger remainder)
//		{
//			if (divisor.sign == 0)
//				throw new DivideByZeroException ();

//			if (dividend.sign == 0) {
//				remainder = dividend;
//				return dividend;
//			}

//			uint[] quotient;
//			uint[] remainder_value;

//			DivModUnsigned (dividend.data, divisor.data, out quotient, out remainder_value);

//			int i;
//			for (i = remainder_value.Length - 1; i >= 0 && remainder_value [i] == 0; --i) ;
//			if (i == -1) {
//				remainder = new BigInteger (0, ZERO);
//			} else {
//				if (i < remainder_value.Length - 1)
//					remainder_value = Resize (remainder_value, i + 1);
//				remainder = new BigInteger (dividend.sign, remainder_value);
//			}

//			for (i = quotient.Length - 1; i >= 0 && quotient [i] == 0; --i) ;
//			if (i == -1)
//				return new BigInteger (0, ZERO);
//			if (i < quotient.Length - 1)
//				quotient = Resize (quotient, i + 1);

//			return new BigInteger ((short)(dividend.sign * divisor.sign), quotient);
//		}

//        public static BigInteger Pow (BigInteger value, int exponent)
//		{
//			if (exponent < 0)
//				throw new ArgumentOutOfRangeException("exponent", "exp must be >= 0");
//			if (exponent == 0)
//				return One;
//			if (exponent == 1)
//				return value;

//			BigInteger result = One;
//			while (exponent != 0) {
//				if ((exponent & 1) != 0)
//					result = result * value;
//				if (exponent == 1)
//					break;

//				value = value * value;
//				exponent >>= 1;
//			}
//			return result;
//        }

//		public static BigInteger ModPow (BigInteger value, BigInteger exponent, BigInteger modulus) {
//			if (exponent.sign == -1)
//				throw new ArgumentOutOfRangeException("exponent", "power must be >= 0");
//			if (modulus.sign == 0)
//				throw new DivideByZeroException ();

//			BigInteger result = One % modulus;
//			while (exponent.sign != 0) {
//				if (!exponent.IsEven) {
//					result = result * value;
//					result = result % modulus;
//				}
//				if (exponent.IsOne)
//					break;
//				value = value * value;
//				value = value % modulus;
//				exponent >>= 1;
//			}
//			return result;
//		}

//		public static BigInteger GreatestCommonDivisor (BigInteger left, BigInteger right)
//		{
//			if (left.data.Length == 1 && left.data [0] == 1)
//				return new BigInteger (1, ONE);
//			if (right.data.Length == 1 && right.data [0] == 1)
//				return new BigInteger (1, ONE);
//			if (left.IsZero)
//				return right;
//			if (right.IsZero)
//				return left;

//			BigInteger x = new BigInteger (1, left.data);
//			BigInteger y = new BigInteger (1, right.data);

//			BigInteger g = y;

//			while (x.data.Length > 1) {
//				g = x;
//				x = y % x;
//				y = g;

//			}
//			if (x.IsZero) return g;

//			// TODO: should we have something here if we can convert to long?

//			//
//			// Now we can just do it with single precision. I am using the binary gcd method,
//			// as it should be faster.
//			//

//			uint yy = x.data [0];
//			uint xx = (uint)(y % yy);

//			int t = 0;

//			while (((xx | yy) & 1) == 0) {
//				xx >>= 1; yy >>= 1; t++;
//			}
//			while (xx != 0) {
//				while ((xx & 1) == 0) xx >>= 1;
//				while ((yy & 1) == 0) yy >>= 1;
//				if (xx >= yy)
//					xx = (xx - yy) >> 1;
//				else
//					yy = (yy - xx) >> 1;
//			}

//			return yy << t;
//		}

//		/*LAMESPEC Log doesn't specify to how many ulp is has to be precise
//		We are equilavent to MS with about 2 ULP
//		*/
//		public static double Log (BigInteger value, Double baseValue)
//		{
//			if (value.sign == -1 || baseValue == 1.0d || baseValue == -1.0d ||
//					baseValue == Double.NegativeInfinity || double.IsNaN (baseValue))
//				return double.NaN;

//			if (baseValue == 0.0d || baseValue == Double.PositiveInfinity)
//				return value.IsOne ? 0 : double.NaN;

//			if (value.sign == 0)
//				return double.NegativeInfinity;

//			int length = value.data.Length - 1;
//			int bitCount = -1;
//			for (int curBit = 31; curBit >= 0; curBit--) {
//				if ((value.data [length] & (1 << curBit)) != 0) {
//					bitCount = curBit + length * 32;
//					break;
//				}
//			}

//			long bitlen = bitCount;
//			Double c = 0, d = 1;

//			BigInteger testBit = One;
//			long tempBitlen = bitlen;
//			while (tempBitlen > Int32.MaxValue) {
//				testBit = testBit << Int32.MaxValue;
//				tempBitlen -= Int32.MaxValue;
//			}
//			testBit = testBit << (int)tempBitlen;

//			for (long curbit = bitlen; curbit >= 0; --curbit) {
//				if ((value & testBit).sign != 0)
//					c += d;
//				d *= 0.5;
//				testBit = testBit >> 1;
//			}
//			return (System.Math.Log (c) + System.Math.Log (2) * bitlen) / System.Math.Log (baseValue);
//		}


//        public static double Log (BigInteger value)
//		{
//            return Log (value, Math.E);
//        }


//        public static double Log10 (BigInteger value)
//		{
//            return Log (value, 10);
//        }

//		[CLSCompliantAttribute (false)]
//		public bool Equals (ulong other)
//		{
//			return CompareTo (other) == 0;
//		}

//		public override int GetHashCode ()
//		{
//			uint hash = (uint)(sign * 0x01010101u);

//			for (int i = 0; i < data.Length; ++i)
//				hash ^=	data [i];
//			return (int)hash;
//		}

//		public static BigInteger Add (BigInteger left, BigInteger right)
//		{
//			return left + right;
//		}

//		public static BigInteger Subtract (BigInteger left, BigInteger right)
//		{
//			return left - right;
//		}

//		public static BigInteger Multiply (BigInteger left, BigInteger right)
//		{
//			return left * right;
//		}

//		public static BigInteger Divide (BigInteger dividend, BigInteger divisor)
//		{
//			return dividend / divisor;
//		}

//		public static BigInteger Remainder (BigInteger dividend, BigInteger divisor)
//		{
//			return dividend % divisor;
//		}

//		public static BigInteger Negate (BigInteger value)
//		{
//			return - value;
//		}

//		public int CompareTo (object obj)
//		{
//			if (obj == null)
//				return 1;

//			if (!(obj is BigInteger))
//				return -1;

//			return Compare (this, (BigInteger)obj);
//		}

//		public int CompareTo (BigInteger other)
//		{
//			return Compare (this, other);
//		}

//		[CLSCompliantAttribute (false)]
//		public int CompareTo (ulong other)
//		{
//			if (sign < 0)
//				return -1;
//			if (sign == 0)
//				return other == 0 ? 0 : -1;

//			if (data.Length > 2)
//				return 1;

//			uint high = (uint)(other >> 32);
//			uint low = (uint)other;

//			return LongCompare (low, high);
//		}

//		int LongCompare (uint low, uint high)
//		{
//			uint h = 0;
//			if (data.Length > 1)
//				h = data [1];

//			if (h > high)
//				return 1;
//			if (h < high)
//				return -1;

//			uint l = data [0];

//			if (l > low)
//				return 1;
//			if (l < low)
//				return -1;

//			return 0;
//		}

//		public int CompareTo (long other)
//		{
//			int ls = sign;
//			int rs = Math.Sign (other);

//			if (ls != rs)
//				return ls > rs ? 1 : -1;

//			if (ls == 0)
//				return 0;

//			if (data.Length > 2)
//				return sign;

//			if (other < 0)
//				other = -other;
//			uint low = (uint)other;
//			uint high = (uint)((ulong)other >> 32);

//			int r = LongCompare (low, high);
//			if (ls == -1)
//				r = -r;

//			return r;
//		}

//		public static int Compare (BigInteger left, BigInteger right)
//		{
//			int ls = left.sign;
//			int rs = right.sign;

//			if (ls != rs)
//				return ls > rs ? 1 : -1;

//			int r = CoreCompare (left.data, right.data);
//			if (ls < 0)
//				r = -r;
//			return r;
//		}


//		static int TopByte (uint x)
//		{
//			if ((x & 0xFFFF0000u) != 0) {
//				if ((x & 0xFF000000u) != 0)
//					return 4;
//				return 3;
//			}
//			if ((x & 0xFF00u) != 0)
//				return 2;
//			return 1;	
//		}

//		static int FirstNonFFByte (uint word)
//		{
//			if ((word & 0xFF000000u) != 0xFF000000u)
//				return 4;
//			else if ((word & 0xFF0000u) != 0xFF0000u)
//				return 3;
//			else if ((word & 0xFF00u) != 0xFF00u)
//				return 2;
//			return 1;
//		}

//		public byte[] ToByteArray ()
//		{
//			if (sign == 0)
//				return new byte [1];

//			//number of bytes not counting upper word
//			int bytes = (data.Length - 1) * 4;
//			bool needExtraZero = false;

//			uint topWord = data [data.Length - 1];
//			int extra;

//			//if the topmost bit is set we need an extra 
//			if (sign == 1) {
//				extra = TopByte (topWord);
//				uint mask = 0x80u << ((extra - 1) * 8);
//				if ((topWord & mask) != 0) {
//					needExtraZero = true;
//				}
//			} else {
//				extra = TopByte (topWord);
//			}

//			byte[] res = new byte [bytes + extra + (needExtraZero ? 1 : 0) ];
//			if (sign == 1) {
//				int j = 0;
//				int end = data.Length - 1;
//				for (int i = 0; i < end; ++i) {
//					uint word = data [i];

//					res [j++] = (byte)word;
//					res [j++] = (byte)(word >> 8);
//					res [j++] = (byte)(word >> 16);
//					res [j++] = (byte)(word >> 24);
//				}
//				while (extra-- > 0) {
//					res [j++] = (byte)topWord;
//					topWord >>= 8;
//				}
//			} else {
//				int j = 0;
//				int end = data.Length - 1;

//				uint carry = 1, word;
//				ulong add;
//				for (int i = 0; i < end; ++i) {
//					word = data [i];
//					add = (ulong)~word + carry;
//					word = (uint)add;
//					carry = (uint)(add >> 32);

//					res [j++] = (byte)word;
//					res [j++] = (byte)(word >> 8);
//					res [j++] = (byte)(word >> 16);
//					res [j++] = (byte)(word >> 24);
//				}

//				add = (ulong)~topWord + (carry);
//				word = (uint)add;
//				carry = (uint)(add >> 32);
//				if (carry == 0) {
//					int ex = FirstNonFFByte (word);
//					bool needExtra = (word & (1 << (ex * 8 - 1))) == 0;
//					int to = ex + (needExtra ? 1 : 0);

//					if (to != extra)
//						res = Resize (res, bytes + to);

//					while (ex-- > 0) {
//						res [j++] = (byte)word;
//						word >>= 8;
//					}
//					if (needExtra)
//						res [j++] = 0xFF;
//				} else {
//					res = Resize (res, bytes + 5);
//					res [j++] = (byte)word;
//					res [j++] = (byte)(word >> 8);
//					res [j++] = (byte)(word >> 16);
//					res [j++] = (byte)(word >> 24);
//					res [j++] = 0xFF;
//				}
//			}

//			return res;
//		}

//		static byte[] Resize (byte[] v, int len)
//		{
//			byte[] res = new byte [len];
//			Array.Copy (v, res, Math.Min (v.Length, len));
//			return res;
//		}

//		static uint[] Resize (uint[] v, int len)
//		{
//			uint[] res = new uint [len];
//			Array.Copy (v, res, Math.Min (v.Length, len));
//			return res;
//		}

//		static uint[] CoreAdd (uint[] a, uint[] b)
//		{
//			if (a.Length < b.Length) {
//				uint[] tmp = a;
//				a = b;
//				b = tmp;
//			}

//			int bl = a.Length;
//			int sl = b.Length;

//			uint[] res = new uint [bl];

//			ulong sum = 0;

//			int i = 0;
//			for (; i < sl; i++) {
//				sum = sum + a [i] + b [i];
//				res [i] = (uint)sum;
//				sum >>= 32;
//			}

//			for (; i < bl; i++) {
//				sum = sum + a [i];
//				res [i] = (uint)sum;
//				sum >>= 32;
//			}

//			if (sum != 0) {
//				res = Resize (res, bl + 1);
//				res [i] = (uint)sum;
//			}

//			return res;
//		}

//		/*invariant a > b*/
//		static uint[] CoreSub (uint[] a, uint[] b)
//		{
//			int bl = a.Length;
//			int sl = b.Length;

//			uint[] res = new uint [bl];

//			ulong borrow = 0;
//			int i;
//			for (i = 0; i < sl; ++i) {
//				borrow = (ulong)a [i] - b [i] - borrow;

//				res [i] = (uint)borrow;
//				borrow = (borrow >> 32) & 0x1;
//			}

//			for (; i < bl; i++) {
//				borrow = (ulong)a [i] - borrow;
//				res [i] = (uint)borrow;
//				borrow = (borrow >> 32) & 0x1;
//			}

//			//remove extra zeroes
//			for (i = bl - 1; i >= 0 && res [i] == 0; --i) ;
//			if (i < bl - 1)
//				res = Resize (res, i + 1);

//            return res;
//		}


//		static uint[] CoreAdd (uint[] a, uint b)
//		{
//			int len = a.Length;
//			uint[] res = new uint [len];

//			ulong sum = b;
//			int i;
//			for (i = 0; i < len; i++) {
//				sum = sum + a [i];
//				res [i] = (uint)sum;
//				sum >>= 32;
//			}

//			if (sum != 0) {
//				res = Resize (res, len + 1);
//				res [i] = (uint)sum;
//			}

//			return res;
//		}

//		static uint[] CoreSub (uint[] a, uint b)
//		{
//			int len = a.Length;
//			uint[] res = new uint [len];

//			ulong borrow = b;
//			int i;
//			for (i = 0; i < len; i++) {
//				borrow = (ulong)a [i] - borrow;
//				res [i] = (uint)borrow;
//				borrow = (borrow >> 32) & 0x1;
//			}

//			//remove extra zeroes
//			for (i = len - 1; i >= 0 && res [i] == 0; --i) ;
//			if (i < len - 1)
//				res = Resize (res, i + 1);

//            return res;
//		}

//		static int CoreCompare (uint[] a, uint[] b)
//		{
//			int	al = a.Length;
//			int bl = b.Length;

//			if (al > bl)
//				return 1;
//			if (bl > al)
//				return -1;

//			for (int i = al - 1; i >= 0; --i) {
//				uint ai = a [i];
//				uint bi = b [i];
//				if (ai > bi)	
//					return 1;
//				if (ai < bi)	
//					return -1;
//			}
//			return 0;
//		}

//		static int GetNormalizeShift(uint value) {
//			int shift = 0;

//			if ((value & 0xFFFF0000) == 0) { value <<= 16; shift += 16; }
//			if ((value & 0xFF000000) == 0) { value <<= 8; shift += 8; }
//			if ((value & 0xF0000000) == 0) { value <<= 4; shift += 4; }
//			if ((value & 0xC0000000) == 0) { value <<= 2; shift += 2; }
//			if ((value & 0x80000000) == 0) { value <<= 1; shift += 1; }

//			return shift;
//		}

//		static void Normalize (uint[] u, int l, uint[] un, int shift)
//		{
//			uint carry = 0;
//			int i;
//			if (shift > 0) {
//				int rshift = 32 - shift;
//				for (i = 0; i < l; i++) {
//					uint ui = u [i];
//					un [i] = (ui << shift) | carry;
//					carry = ui >> rshift;
//				}
//			} else {
//				for (i = 0; i < l; i++) {
//					un [i] = u [i];
//				}
//			}

//			while (i < un.Length) {
//				un [i++] = 0;
//			}

//			if (carry != 0) {
//				un [l] = carry;
//			}
//		}

//		static void Unnormalize (uint[] un, out uint[] r, int shift)
//		{
//			int length = un.Length;
//			r = new uint [length];

//			if (shift > 0) {
//				int lshift = 32 - shift;
//				uint carry = 0;
//				for (int i = length - 1; i >= 0; i--) {
//					uint uni = un [i];
//					r [i] = (uni >> shift) | carry;
//					carry = (uni << lshift);
//				}
//			} else {
//				for (int i = 0; i < length; i++) {
//					r [i] = un [i];
//				}
//			}
//		}

//		const ulong Base = 0x100000000;
//		static void DivModUnsigned (uint[] u, uint[] v, out uint[] q, out uint[] r)
//		{
//			int m = u.Length;
//			int n = v.Length;

//			if (n <= 1) {
//				//  Divide by single digit
//				//
//				ulong rem = 0;
//				uint v0 = v [0];
//				q = new uint[m];
//				r = new uint [1];

//				for (int j = m - 1; j >= 0; j--) {
//					rem *= Base;
//					rem += u[j];

//					ulong div = rem / v0;
//					rem -= div * v0;
//					q[j] = (uint)div;
//				}
//				r [0] = (uint)rem;
//			} else if (m >= n) {
//				int shift = GetNormalizeShift (v [n - 1]);

//				uint[] un = new uint [m + 1];
//				uint[] vn = new uint [n];

//				Normalize (u, m, un, shift);
//				Normalize (v, n, vn, shift);

//				q = new uint [m - n + 1];
//				r = null;

//				//  Main division loop
//				//
//				for (int j = m - n; j >= 0; j--) {
//					ulong rr, qq;
//					int i;

//					rr = Base * un [j + n] + un [j + n - 1];
//					qq = rr / vn [n - 1];
//					rr -= qq * vn [n - 1];

//					for (; ; ) {
//						// Estimate too big ?
//						//
//						if ((qq >= Base) || (qq * vn [n - 2] > (rr * Base + un [j + n - 2]))) {
//							qq--;
//							rr += (ulong)vn [n - 1];
//							if (rr < Base)
//								continue;
//						}
//						break;
//					}


//					//  Multiply and subtract
//					//
//					long b = 0;
//					long t = 0;
//					for (i = 0; i < n; i++) {
//						ulong p = vn [i] * qq;
//						t = (long)un [i + j] - (long)(uint)p - b;
//						un [i + j] = (uint)t;
//						p >>= 32;
//						t >>= 32;
//						b = (long)p - t;
//					}
//					t = (long)un [j + n] - b;
//					un [j + n] = (uint)t;

//					//  Store the calculated value
//					//
//					q [j] = (uint)qq;

//					//  Add back vn[0..n] to un[j..j+n]
//					//
//					if (t < 0) {
//						q [j]--;
//						ulong c = 0;
//						for (i = 0; i < n; i++) {
//							c = (ulong)vn [i] + un [j + i] + c;
//							un [j + i] = (uint)c;
//							c >>= 32;
//						}
//						c += (ulong)un [j + n];
//						un [j + n] = (uint)c;
//					}
//				}

//				Unnormalize (un, out r, shift);
//			} else {
//				q = new uint [] { 0 };
//				r = u;
//			}
//		}
//	}
//}


//public struct BigInteger : IFormattable, IComparable, IComparable<BigInteger>, IEquatable<BigInteger>
//{
//    private const int knMaskHighBit = int.MinValue;
//    private const uint kuMaskHighBit = unchecked((uint)int.MinValue);
//    private const int kcbitUint = 32;
//    private const int kcbitUlong = 64;
//    private const int DecimalScaleFactorMask = 0x00FF0000;
//    private const int DecimalSignMask = unchecked((int)0x80000000);

//    // For values int.MinValue < n <= int.MaxValue, the value is stored in sign
//    // and _bits is null. For all other values, sign is +1 or -1 and the bits are in _bits
//    internal readonly int _sign; // Do not rename (binary serialization)
//    internal readonly uint[] _bits; // Do not rename (binary serialization)

//    // We have to make a choice of how to represent int.MinValue. This is the one
//    // value that fits in an int, but whose negation does not fit in an int.
//    // We choose to use a large representation, so we're symmetric with respect to negation.
//    private static readonly BigInteger s_bnMinInt = new BigInteger(-1, new uint[] { kuMaskHighBit });
//    private static readonly BigInteger s_bnOneInt = new BigInteger(1);
//    private static readonly BigInteger s_bnZeroInt = new BigInteger(0);
//    private static readonly BigInteger s_bnMinusOneInt = new BigInteger(-1);

//    public BigInteger(int value)
//    {
//        if (value == int.MinValue)
//            this = s_bnMinInt;
//        else
//        {
//            _sign = value;
//            _bits = null;
//        }
//        AssertValid();
//    }

//    [CLSCompliant(false)]
//    public BigInteger(uint value)
//    {
//        if (value <= int.MaxValue)
//        {
//            _sign = (int)value;
//            _bits = null;
//        }
//        else
//        {
//            _sign = +1;
//            _bits = new uint[1];
//            _bits[0] = value;
//        }
//        AssertValid();
//    }

//    public BigInteger(long value)
//    {
//        if (int.MinValue < value && value <= int.MaxValue)
//        {
//            _sign = (int)value;
//            _bits = null;
//        }
//        else if (value == int.MinValue)
//        {
//            this = s_bnMinInt;
//        }
//        else
//        {
//            ulong x = 0;
//            if (value < 0)
//            {
//                x = unchecked((ulong)-value);
//                _sign = -1;
//            }
//            else
//            {
//                x = (ulong)value;
//                _sign = +1;
//            }

//            if (x <= uint.MaxValue)
//            {
//                _bits = new uint[1];
//                _bits[0] = (uint)x;
//            }
//            else
//            {
//                _bits = new uint[2];
//                _bits[0] = unchecked((uint)x);
//                _bits[1] = (uint)(x >> kcbitUint);
//            }
//        }

//        AssertValid();
//    }

//    [CLSCompliant(false)]
//    public BigInteger(ulong value)
//    {
//        if (value <= int.MaxValue)
//        {
//            _sign = (int)value;
//            _bits = null;
//        }
//        else if (value <= uint.MaxValue)
//        {
//            _sign = +1;
//            _bits = new uint[1];
//            _bits[0] = (uint)value;
//        }
//        else
//        {
//            _sign = +1;
//            _bits = new uint[2];
//            _bits[0] = unchecked((uint)value);
//            _bits[1] = (uint)(value >> kcbitUint);
//        }

//        AssertValid();
//    }

//    public BigInteger(float value) : this((double)value)
//    {
//    }

//    public BigInteger(double value)
//    {
//        if (double.IsInfinity(value))
//            throw new OverflowException(SR.Overflow_BigIntInfinity);
//        if (double.IsNaN(value))
//            throw new OverflowException(SR.Overflow_NotANumber);

//        _sign = 0;
//        _bits = null;

//        int sign, exp;
//        ulong man;
//        bool fFinite;
//        NumericsHelpers.GetDoubleParts(value, out sign, out exp, out man, out fFinite);
//        Debug.Assert(sign == +1 || sign == -1);

//        if (man == 0)
//        {
//            this = Zero;
//            return;
//        }

//        Debug.Assert(man < (1UL << 53));
//        Debug.Assert(exp <= 0 || man >= (1UL << 52));

//        if (exp <= 0)
//        {
//            if (exp <= -kcbitUlong)
//            {
//                this = Zero;
//                return;
//            }
//            this = man >> -exp;
//            if (sign < 0)
//                _sign = -_sign;
//        }
//        else if (exp <= 11)
//        {
//            this = man << exp;
//            if (sign < 0)
//                _sign = -_sign;
//        }
//        else
//        {
//            // Overflow into at least 3 uints.
//            // Move the leading 1 to the high bit.
//            man <<= 11;
//            exp -= 11;

//            // Compute cu and cbit so that exp == 32 * cu - cbit and 0 <= cbit < 32.
//            int cu = (exp - 1) / kcbitUint + 1;
//            int cbit = cu * kcbitUint - exp;
//            Debug.Assert(0 <= cbit && cbit < kcbitUint);
//            Debug.Assert(cu >= 1);

//            // Populate the uints.
//            _bits = new uint[cu + 2];
//            _bits[cu + 1] = (uint)(man >> (cbit + kcbitUint));
//            _bits[cu] = unchecked((uint)(man >> cbit));
//            if (cbit > 0)
//                _bits[cu - 1] = unchecked((uint)man) << (kcbitUint - cbit);
//            _sign = sign;
//        }

//        AssertValid();
//    }

//    public BigInteger(decimal value)
//    {
//        // First truncate to get scale to 0 and extract bits
//        int[] bits = decimal.GetBits(decimal.Truncate(value));

//        Debug.Assert(bits.Length == 4 && (bits[3] & DecimalScaleFactorMask) == 0);

//        int size = 3;
//        while (size > 0 && bits[size - 1] == 0)
//            size--;
//        if (size == 0)
//        {
//            this = s_bnZeroInt;
//        }
//        else if (size == 1 && bits[0] > 0)
//        {
//            // bits[0] is the absolute value of this decimal
//            // if bits[0] < 0 then it is too large to be packed into _sign
//            _sign = bits[0];
//            _sign *= ((bits[3] & DecimalSignMask) != 0) ? -1 : +1;
//            _bits = null;
//        }
//        else
//        {
//            _bits = new uint[size];

//            unchecked
//            {
//                _bits[0] = (uint)bits[0];
//                if (size > 1)
//                    _bits[1] = (uint)bits[1];
//                if (size > 2)
//                    _bits[2] = (uint)bits[2];
//            }

//            _sign = ((bits[3] & DecimalSignMask) != 0) ? -1 : +1;
//        }
//        AssertValid();
//    }

//    /// <summary>
//    /// Creates a BigInteger from a little-endian twos-complement byte array.
//    /// </summary>
//    /// <param name="value"></param>
//    [CLSCompliant(false)]
//    public BigInteger(byte[] value)
//    {
//        if (value == null)
//            throw new ArgumentNullException(nameof(value));

//        int byteCount = value.Length;
//        bool isNegative = byteCount > 0 && ((value[byteCount - 1] & 0x80) == 0x80);

//        // Try to conserve space as much as possible by checking for wasted leading byte[] entries 
//        while (byteCount > 0 && value[byteCount - 1] == 0) byteCount--;

//        if (byteCount == 0)
//        {
//            // BigInteger.Zero
//            _sign = 0;
//            _bits = null;
//            AssertValid();
//            return;
//        }

//        if (byteCount <= 4)
//        {
//            if (isNegative)
//                _sign = unchecked((int)0xffffffff);
//            else
//                _sign = 0;
//            for (int i = byteCount - 1; i >= 0; i--)
//            {
//                _sign <<= 8;
//                _sign |= value[i];
//            }
//            _bits = null;

//            if (_sign < 0 && !isNegative)
//            {
//                // Int32 overflow
//                // Example: Int64 value 2362232011 (0xCB, 0xCC, 0xCC, 0x8C, 0x0)
//                // can be naively packed into 4 bytes (due to the leading 0x0)
//                // it overflows into the int32 sign bit
//                _bits = new uint[1];
//                _bits[0] = unchecked((uint)_sign);
//                _sign = +1;
//            }
//            if (_sign == int.MinValue)
//                this = s_bnMinInt;
//        }
//        else
//        {
//            int unalignedBytes = byteCount % 4;
//            int dwordCount = byteCount / 4 + (unalignedBytes == 0 ? 0 : 1);
//            bool isZero = true;
//            uint[] val = new uint[dwordCount];

//            // Copy all dwords, except but don't do the last one if it's not a full four bytes
//            int curDword, curByte, byteInDword;
//            curByte = 3;
//            for (curDword = 0; curDword < dwordCount - (unalignedBytes == 0 ? 0 : 1); curDword++)
//            {
//                byteInDword = 0;
//                while (byteInDword < 4)
//                {
//                    if (value[curByte] != 0x00) isZero = false;
//                    val[curDword] <<= 8;
//                    val[curDword] |= value[curByte];
//                    curByte--;
//                    byteInDword++;
//                }
//                curByte += 8;
//            }

//            // Copy the last dword specially if it's not aligned
//            if (unalignedBytes != 0)
//            {
//                if (isNegative) val[dwordCount - 1] = 0xffffffff;
//                for (curByte = byteCount - 1; curByte >= byteCount - unalignedBytes; curByte--)
//                {
//                    if (value[curByte] != 0x00) isZero = false;
//                    val[curDword] <<= 8;
//                    val[curDword] |= value[curByte];
//                }
//            }

//            if (isZero)
//            {
//                this = s_bnZeroInt;
//            }
//            else if (isNegative)
//            {
//                NumericsHelpers.DangerousMakeTwosComplement(val); // Mutates val

//                // Pack _bits to remove any wasted space after the twos complement
//                int len = val.Length;
//                while (len > 0 && val[len - 1] == 0)
//                    len--;
//                if (len == 1 && unchecked((int)(val[0])) > 0)
//                {
//                    if (val[0] == 1 /* abs(-1) */)
//                    {
//                        this = s_bnMinusOneInt;
//                    }
//                    else if (val[0] == kuMaskHighBit /* abs(Int32.MinValue) */)
//                    {
//                        this = s_bnMinInt;
//                    }
//                    else
//                    {
//                        _sign = (-1) * ((int)val[0]);
//                        _bits = null;
//                    }
//                }
//                else if (len != val.Length)
//                {
//                    _sign = -1;
//                    _bits = new uint[len];
//                    Array.Copy(val, 0, _bits, 0, len);
//                }
//                else
//                {
//                    _sign = -1;
//                    _bits = val;
//                }
//            }
//            else
//            {
//                _sign = +1;
//                _bits = val;
//            }
//        }
//        AssertValid();
//    }

//    internal BigInteger(int n, uint[] rgu)
//    {
//        _sign = n;
//        _bits = rgu;
//        AssertValid();
//    }

//    /// <summary>
//    /// Constructor used during bit manipulation and arithmetic.
//    /// When possible the uint[] will be packed into  _sign to conserve space.
//    /// </summary>
//    /// <param name="value">The absolute value of the number</param>
//    /// <param name="negative">The bool indicating the sign of the value.</param>
//    internal BigInteger(uint[] value, bool negative)
//    {
//        if (value == null)
//            throw new ArgumentNullException(nameof(value));

//        int len;

//        // Try to conserve space as much as possible by checking for wasted leading uint[] entries 
//        // sometimes the uint[] has leading zeros from bit manipulation operations & and ^
//        for (len = value.Length; len > 0 && value[len - 1] == 0; len--) ;

//        if (len == 0)
//            this = s_bnZeroInt;
//        // Values like (Int32.MaxValue+1) are stored as "0x80000000" and as such cannot be packed into _sign
//        else if (len == 1 && value[0] < kuMaskHighBit)
//        {
//            _sign = (negative ? -(int)value[0] : (int)value[0]);
//            _bits = null;
//            // Although Int32.MinValue fits in _sign, we represent this case differently for negate
//            if (_sign == int.MinValue)
//                this = s_bnMinInt;
//        }
//        else
//        {
//            _sign = negative ? -1 : +1;
//            _bits = new uint[len];
//            Array.Copy(value, 0, _bits, 0, len);
//        }
//        AssertValid();
//    }

//    /// <summary>
//    /// Create a BigInteger from a little-endian twos-complement UInt32 array.
//    /// When possible, value is assigned directly to this._bits without an array copy
//    /// so use this ctor with care.
//    /// </summary>
//    /// <param name="value"></param>
//    private BigInteger(uint[] value)
//    {
//        if (value == null)
//            throw new ArgumentNullException(nameof(value));

//        int dwordCount = value.Length;
//        bool isNegative = dwordCount > 0 && ((value[dwordCount - 1] & 0x80000000) == 0x80000000);

//        // Try to conserve space as much as possible by checking for wasted leading uint[] entries 
//        while (dwordCount > 0 && value[dwordCount - 1] == 0) dwordCount--;

//        if (dwordCount == 0)
//        {
//            // BigInteger.Zero
//            this = s_bnZeroInt;
//            AssertValid();
//            return;
//        }
//        if (dwordCount == 1)
//        {
//            if (unchecked((int)value[0]) < 0 && !isNegative)
//            {
//                _bits = new uint[1];
//                _bits[0] = value[0];
//                _sign = +1;
//            }
//            // Handle the special cases where the BigInteger likely fits into _sign
//            else if (int.MinValue == unchecked((int)value[0]))
//            {
//                this = s_bnMinInt;
//            }
//            else
//            {
//                _sign = unchecked((int)value[0]);
//                _bits = null;
//            }
//            AssertValid();
//            return;
//        }

//        if (!isNegative)
//        {
//            // Handle the simple positive value cases where the input is already in sign magnitude
//            if (dwordCount != value.Length)
//            {
//                _sign = +1;
//                _bits = new uint[dwordCount];
//                Array.Copy(value, 0, _bits, 0, dwordCount);
//            }
//            // No trimming is possible.  Assign value directly to _bits.  
//            else
//            {
//                _sign = +1;
//                _bits = value;
//            }
//            AssertValid();
//            return;
//        }

//        // Finally handle the more complex cases where we must transform the input into sign magnitude
//        NumericsHelpers.DangerousMakeTwosComplement(value); // mutates val

//        // Pack _bits to remove any wasted space after the twos complement
//        int len = value.Length;
//        while (len > 0 && value[len - 1] == 0) len--;

//        // The number is represented by a single dword
//        if (len == 1 && unchecked((int)(value[0])) > 0)
//        {
//            if (value[0] == 1 /* abs(-1) */)
//            {
//                this = s_bnMinusOneInt;
//            }
//            else if (value[0] == kuMaskHighBit /* abs(Int32.MinValue) */)
//            {
//                this = s_bnMinInt;
//            }
//            else
//            {
//                _sign = (-1) * ((int)value[0]);
//                _bits = null;
//            }
//        }
//        // The number is represented by multiple dwords.
//        // Trim off any wasted uint values when possible.
//        else if (len != value.Length)
//        {
//            _sign = -1;
//            _bits = new uint[len];
//            Array.Copy(value, 0, _bits, 0, len);
//        }
//        // No trimming is possible.  Assign value directly to _bits.  
//        else
//        {
//            _sign = -1;
//            _bits = value;
//        }
//        AssertValid();
//        return;
//    }

//    public static BigInteger Zero { get { return s_bnZeroInt; } }

//    public static BigInteger One { get { return s_bnOneInt; } }

//    public static BigInteger MinusOne { get { return s_bnMinusOneInt; } }

//    public bool IsPowerOfTwo
//    {
//        get
//        {
//            AssertValid();

//            if (_bits == null)
//                return (_sign & (_sign - 1)) == 0 && _sign != 0;

//            if (_sign != 1)
//                return false;
//            int iu = _bits.Length - 1;
//            if ((_bits[iu] & (_bits[iu] - 1)) != 0)
//                return false;
//            while (--iu >= 0)
//            {
//                if (_bits[iu] != 0)
//                    return false;
//            }
//            return true;
//        }
//    }

//    public bool IsZero { get { AssertValid(); return _sign == 0; } }

//    public bool IsOne { get { AssertValid(); return _sign == 1 && _bits == null; } }

//    public bool IsEven { get { AssertValid(); return _bits == null ? (_sign & 1) == 0 : (_bits[0] & 1) == 0; } }

//    public int Sign
//    {
//        get { AssertValid(); return (_sign >> (kcbitUint - 1)) - (-_sign >> (kcbitUint - 1)); }
//    }

//    public static BigInteger Parse(string value)
//    {
//        return Parse(value, NumberStyles.Integer);
//    }

//    public static BigInteger Parse(string value, NumberStyles style)
//    {
//        return Parse(value, style, NumberFormatInfo.CurrentInfo);
//    }

//    public static BigInteger Parse(string value, IFormatProvider provider)
//    {
//        return Parse(value, NumberStyles.Integer, NumberFormatInfo.GetInstance(provider));
//    }

//    public static BigInteger Parse(string value, NumberStyles style, IFormatProvider provider)
//    {
//        return BigNumber.ParseBigInteger(value, style, NumberFormatInfo.GetInstance(provider));
//    }

//    public static bool TryParse(string value, out BigInteger result)
//    {
//        return TryParse(value, NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out result);
//    }

//    public static bool TryParse(string value, NumberStyles style, IFormatProvider provider, out BigInteger result)
//    {
//        return BigNumber.TryParseBigInteger(value, style, NumberFormatInfo.GetInstance(provider), out result);
//    }

//    public static int Compare(BigInteger left, BigInteger right)
//    {
//        return left.CompareTo(right);
//    }

//    public static BigInteger Abs(BigInteger value)
//    {
//        return (value >= Zero) ? value : -value;
//    }

//    public static BigInteger Add(BigInteger left, BigInteger right)
//    {
//        return left + right;
//    }

//    public static BigInteger Subtract(BigInteger left, BigInteger right)
//    {
//        return left - right;
//    }

//    public static BigInteger Multiply(BigInteger left, BigInteger right)
//    {
//        return left * right;
//    }

//    public static BigInteger Divide(BigInteger dividend, BigInteger divisor)
//    {
//        return dividend / divisor;
//    }

//    public static BigInteger Remainder(BigInteger dividend, BigInteger divisor)
//    {
//        return dividend % divisor;
//    }

//    public static BigInteger DivRem(BigInteger dividend, BigInteger divisor, out BigInteger remainder)
//    {
//        dividend.AssertValid();
//        divisor.AssertValid();

//        bool trivialDividend = dividend._bits == null;
//        bool trivialDivisor = divisor._bits == null;

//        if (trivialDividend && trivialDivisor)
//        {
//            remainder = dividend._sign % divisor._sign;
//            return dividend._sign / divisor._sign;
//        }

//        if (trivialDividend)
//        {
//            // The divisor is non-trivial
//            // and therefore the bigger one
//            remainder = dividend;
//            return s_bnZeroInt;
//        }

//        if (trivialDivisor)
//        {
//            uint rest;
//            uint[] bits = BigIntegerCalculator.Divide(dividend._bits, NumericsHelpers.Abs(divisor._sign), out rest);

//            remainder = dividend._sign < 0 ? -1 * rest : rest;
//            return new BigInteger(bits, (dividend._sign < 0) ^ (divisor._sign < 0));
//        }

//        if (dividend._bits.Length < divisor._bits.Length)
//        {
//            remainder = dividend;
//            return s_bnZeroInt;
//        }
//        else
//        {
//            uint[] rest;
//            uint[] bits = BigIntegerCalculator.Divide(dividend._bits, divisor._bits, out rest);

//            remainder = new BigInteger(rest, dividend._sign < 0);
//            return new BigInteger(bits, (dividend._sign < 0) ^ (divisor._sign < 0));
//        }
//    }

//    public static BigInteger Negate(BigInteger value)
//    {
//        return -value;
//    }

//    public static double Log(BigInteger value)
//    {
//        return Log(value, Math.E);
//    }

//    public static double Log(BigInteger value, double baseValue)
//    {
//        if (value._sign < 0 || baseValue == 1.0D)
//            return double.NaN;
//        if (baseValue == double.PositiveInfinity)
//            return value.IsOne ? 0.0D : double.NaN;
//        if (baseValue == 0.0D && !value.IsOne)
//            return double.NaN;
//        if (value._bits == null)
//            return Math.Log(value._sign, baseValue);

//        ulong h = value._bits[value._bits.Length - 1];
//        ulong m = value._bits.Length > 1 ? value._bits[value._bits.Length - 2] : 0;
//        ulong l = value._bits.Length > 2 ? value._bits[value._bits.Length - 3] : 0;

//        // Measure the exact bit count
//        int c = NumericsHelpers.CbitHighZero((uint)h);
//        long b = (long)value._bits.Length * 32 - c;

//        // Extract most significant bits
//        ulong x = (h << 32 + c) | (m << c) | (l >> 32 - c);

//        // Let v = value, b = bit count, x = v/2^b-64
//        // log ( v/2^b-64 * 2^b-64 ) = log ( x ) + log ( 2^b-64 )
//        return Math.Log(x, baseValue) + (b - 64) / Math.Log(baseValue, 2);
//    }

//    public static double Log10(BigInteger value)
//    {
//        return Log(value, 10);
//    }

//    public static BigInteger GreatestCommonDivisor(BigInteger left, BigInteger right)
//    {
//        left.AssertValid();
//        right.AssertValid();

//        bool trivialLeft = left._bits == null;
//        bool trivialRight = right._bits == null;

//        if (trivialLeft && trivialRight)
//        {
//            return BigIntegerCalculator.Gcd(NumericsHelpers.Abs(left._sign), NumericsHelpers.Abs(right._sign));
//        }

//        if (trivialLeft)
//        {
//            return left._sign != 0
//                ? BigIntegerCalculator.Gcd(right._bits, NumericsHelpers.Abs(left._sign))
//                : new BigInteger(right._bits, false);
//        }

//        if (trivialRight)
//        {
//            return right._sign != 0
//                ? BigIntegerCalculator.Gcd(left._bits, NumericsHelpers.Abs(right._sign))
//                : new BigInteger(left._bits, false);
//        }

//        if (BigIntegerCalculator.Compare(left._bits, right._bits) < 0)
//        {
//            return GreatestCommonDivisor(right._bits, left._bits);
//        }
//        else
//        {
//            return GreatestCommonDivisor(left._bits, right._bits);
//        }
//    }

//    private static BigInteger GreatestCommonDivisor(uint[] leftBits, uint[] rightBits)
//    {
//        Debug.Assert(BigIntegerCalculator.Compare(leftBits, rightBits) >= 0);

//        // Short circuits to spare some allocations...
//        if (rightBits.Length == 1)
//        {
//            uint temp = BigIntegerCalculator.Remainder(leftBits, rightBits[0]);
//            return BigIntegerCalculator.Gcd(rightBits[0], temp);
//        }

//        if (rightBits.Length == 2)
//        {
//            uint[] tempBits = BigIntegerCalculator.Remainder(leftBits, rightBits);

//            ulong left = ((ulong)rightBits[1] << 32) | rightBits[0];
//            ulong right = ((ulong)tempBits[1] << 32) | tempBits[0];

//            return BigIntegerCalculator.Gcd(left, right);
//        }

//        uint[] bits = BigIntegerCalculator.Gcd(leftBits, rightBits);
//        return new BigInteger(bits, false);
//    }

//    public static BigInteger Max(BigInteger left, BigInteger right)
//    {
//        if (left.CompareTo(right) < 0)
//            return right;
//        return left;
//    }

//    public static BigInteger Min(BigInteger left, BigInteger right)
//    {
//        if (left.CompareTo(right) <= 0)
//            return left;
//        return right;
//    }

//    public static BigInteger ModPow(BigInteger value, BigInteger exponent, BigInteger modulus)
//    {
//        if (exponent.Sign < 0)
//            throw new ArgumentOutOfRangeException(nameof(exponent), SR.ArgumentOutOfRange_MustBeNonNeg);

//        value.AssertValid();
//        exponent.AssertValid();
//        modulus.AssertValid();

//        bool trivialValue = value._bits == null;
//        bool trivialExponent = exponent._bits == null;
//        bool trivialModulus = modulus._bits == null;

//        if (trivialModulus)
//        {
//            uint bits = trivialValue && trivialExponent ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent._sign), NumericsHelpers.Abs(modulus._sign)) :
//                        trivialValue ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), exponent._bits, NumericsHelpers.Abs(modulus._sign)) :
//                        trivialExponent ? BigIntegerCalculator.Pow(value._bits, NumericsHelpers.Abs(exponent._sign), NumericsHelpers.Abs(modulus._sign)) :
//                        BigIntegerCalculator.Pow(value._bits, exponent._bits, NumericsHelpers.Abs(modulus._sign));

//            return value._sign < 0 && !exponent.IsEven ? -1 * bits : bits;
//        }
//        else
//        {
//            uint[] bits = trivialValue && trivialExponent ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent._sign), modulus._bits) :
//                          trivialValue ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), exponent._bits, modulus._bits) :
//                          trivialExponent ? BigIntegerCalculator.Pow(value._bits, NumericsHelpers.Abs(exponent._sign), modulus._bits) :
//                          BigIntegerCalculator.Pow(value._bits, exponent._bits, modulus._bits);

//            return new BigInteger(bits, value._sign < 0 && !exponent.IsEven);
//        }
//    }

//    public static BigInteger Pow(BigInteger value, int exponent)
//    {
//        if (exponent < 0)
//            throw new ArgumentOutOfRangeException(nameof(exponent), SR.ArgumentOutOfRange_MustBeNonNeg);

//        value.AssertValid();

//        if (exponent == 0)
//            return s_bnOneInt;
//        if (exponent == 1)
//            return value;

//        bool trivialValue = value._bits == null;

//        if (trivialValue)
//        {
//            if (value._sign == 1)
//                return value;
//            if (value._sign == -1)
//                return (exponent & 1) != 0 ? value : s_bnOneInt;
//            if (value._sign == 0)
//                return value;
//        }

//        uint[] bits = trivialValue
//                    ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent))
//                    : BigIntegerCalculator.Pow(value._bits, NumericsHelpers.Abs(exponent));

//        return new BigInteger(bits, value._sign < 0 && (exponent & 1) != 0);
//    }

//    public override int GetHashCode()
//    {
//        AssertValid();

//        if (_bits == null)
//            return _sign;
//        int hash = _sign;
//        for (int iv = _bits.Length; --iv >= 0;)
//            hash = NumericsHelpers.CombineHash(hash, unchecked((int)_bits[iv]));
//        return hash;
//    }

//    public override bool Equals(object obj)
//    {
//        AssertValid();

//        if (!(obj is BigInteger))
//            return false;
//        return Equals((BigInteger)obj);
//    }

//    public bool Equals(long other)
//    {
//        AssertValid();

//        if (_bits == null)
//            return _sign == other;

//        int cu;
//        if ((_sign ^ other) < 0 || (cu = _bits.Length) > 2)
//            return false;

//        ulong uu = other < 0 ? (ulong)-other : (ulong)other;
//        if (cu == 1)
//            return _bits[0] == uu;

//        return NumericsHelpers.MakeUlong(_bits[1], _bits[0]) == uu;
//    }

//    [CLSCompliant(false)]
//    public bool Equals(ulong other)
//    {
//        AssertValid();

//        if (_sign < 0)
//            return false;
//        if (_bits == null)
//            return (ulong)_sign == other;

//        int cu = _bits.Length;
//        if (cu > 2)
//            return false;
//        if (cu == 1)
//            return _bits[0] == other;
//        return NumericsHelpers.MakeUlong(_bits[1], _bits[0]) == other;
//    }

//    public bool Equals(BigInteger other)
//    {
//        AssertValid();
//        other.AssertValid();

//        if (_sign != other._sign)
//            return false;
//        if (_bits == other._bits)
//            // _sign == other._sign && _bits == null && other._bits == null
//            return true;

//        if (_bits == null || other._bits == null)
//            return false;
//        int cu = _bits.Length;
//        if (cu != other._bits.Length)
//            return false;
//        int cuDiff = GetDiffLength(_bits, other._bits, cu);
//        return cuDiff == 0;
//    }

//    public int CompareTo(long other)
//    {
//        AssertValid();

//        if (_bits == null)
//            return ((long)_sign).CompareTo(other);
//        int cu;
//        if ((_sign ^ other) < 0 || (cu = _bits.Length) > 2)
//            return _sign;
//        ulong uu = other < 0 ? (ulong)-other : (ulong)other;
//        ulong uuTmp = cu == 2 ? NumericsHelpers.MakeUlong(_bits[1], _bits[0]) : _bits[0];
//        return _sign * uuTmp.CompareTo(uu);
//    }

//    [CLSCompliant(false)]
//    public int CompareTo(ulong other)
//    {
//        AssertValid();

//        if (_sign < 0)
//            return -1;
//        if (_bits == null)
//            return ((ulong)_sign).CompareTo(other);
//        int cu = _bits.Length;
//        if (cu > 2)
//            return +1;
//        ulong uuTmp = cu == 2 ? NumericsHelpers.MakeUlong(_bits[1], _bits[0]) : _bits[0];
//        return uuTmp.CompareTo(other);
//    }

//    public int CompareTo(BigInteger other)
//    {
//        AssertValid();
//        other.AssertValid();

//        if ((_sign ^ other._sign) < 0)
//        {
//            // Different signs, so the comparison is easy.
//            return _sign < 0 ? -1 : +1;
//        }

//        // Same signs
//        if (_bits == null)
//        {
//            if (other._bits == null)
//                return _sign < other._sign ? -1 : _sign > other._sign ? +1 : 0;
//            return -other._sign;
//        }
//        int cuThis, cuOther;
//        if (other._bits == null || (cuThis = _bits.Length) > (cuOther = other._bits.Length))
//            return _sign;
//        if (cuThis < cuOther)
//            return -_sign;

//        int cuDiff = GetDiffLength(_bits, other._bits, cuThis);
//        if (cuDiff == 0)
//            return 0;
//        return _bits[cuDiff - 1] < other._bits[cuDiff - 1] ? -_sign : _sign;
//    }

//    public int CompareTo(object obj)
//    {
//        if (obj == null)
//            return 1;
//        if (!(obj is BigInteger))
//            throw new ArgumentException(SR.Argument_MustBeBigInt);
//        return CompareTo((BigInteger)obj);
//    }

//    /// <summary>
//    /// Returns the value of this BigInteger as a little-endian twos-complement
//    /// byte array, using the fewest number of bytes possible. If the value is zero,
//    /// return an array of one byte whose element is 0x00.
//    /// </summary>
//    /// <returns></returns>
//    public byte[] ToByteArray()
//    {
//        int sign = _sign;
//        if (sign == 0)
//        {
//            return new byte[] { 0 };
//        }

//        byte highByte;
//        int nonZeroDwordIndex = 0;
//        uint highDword;
//        uint[] bits = _bits;
//        if (bits == null)
//        {
//            highByte = (byte)((sign < 0) ? 0xff : 0x00);
//            highDword = unchecked((uint)sign);
//        }
//        else if (sign == -1)
//        {
//            highByte = 0xff;

//            // If sign is -1, we will need to two's complement bits.
//            // Previously this was accomplished via NumericsHelpers.DangerousMakeTwosComplement(),
//            // however, we can do the two's complement on the stack so as to avoid
//            // creating a temporary copy of bits just to hold the two's complement.
//            // One special case in DangerousMakeTwosComplement() is that if the array
//            // is all zeros, then it would allocate a new array with the high-order
//            // uint set to 1 (for the carry). In our usage, we will not hit this case
//            // because a bits array of all zeros would represent 0, and this case
//            // would be encoded as _bits = null and _sign = 0.
//            Debug.Assert(bits.Length > 0);
//            Debug.Assert(bits[bits.Length - 1] != 0);
//            while (bits[nonZeroDwordIndex] == 0U)
//            {
//                nonZeroDwordIndex++;
//            }

//            highDword = ~bits[bits.Length - 1];
//            if (bits.Length - 1 == nonZeroDwordIndex)
//            {
//                // This will not overflow because highDword is less than or equal to uint.MaxValue - 1.
//                Debug.Assert(highDword <= uint.MaxValue - 1);
//                highDword += 1U;
//            }
//        }
//        else
//        {
//            Debug.Assert(sign == 1);
//            highByte = 0x00;
//            highDword = bits[bits.Length - 1];
//        }

//        byte msb;
//        int msbIndex;
//        if ((msb = unchecked((byte)(highDword >> 24))) != highByte)
//        {
//            msbIndex = 3;
//        }
//        else if ((msb = unchecked((byte)(highDword >> 16))) != highByte)
//        {
//            msbIndex = 2;
//        }
//        else if ((msb = unchecked((byte)(highDword >> 8))) != highByte)
//        {
//            msbIndex = 1;
//        }
//        else
//        {
//            msb = unchecked((byte)highDword);
//            msbIndex = 0;
//        }

//        // Ensure high bit is 0 if positive, 1 if negative
//        bool needExtraByte = (msb & 0x80) != (highByte & 0x80);
//        byte[] bytes;
//        int curByte = 0;
//        if (bits == null)
//        {
//            bytes = new byte[msbIndex + 1 + (needExtraByte ? 1 : 0)];
//            Debug.Assert(bytes.Length <= 4);
//        }
//        else
//        {
//            bytes = new byte[checked(4 * (bits.Length - 1) + msbIndex + 1 + (needExtraByte ? 1 : 0))];

//            for (int i = 0; i < bits.Length - 1; i++)
//            {
//                uint dword = bits[i];
//                if (sign == -1)
//                {
//                    dword = ~dword;
//                    if (i <= nonZeroDwordIndex)
//                    {
//                        dword = unchecked(dword + 1U);
//                    }
//                }
//                for (int j = 0; j < 4; j++)
//                {
//                    bytes[curByte++] = unchecked((byte)dword);
//                    dword >>= 8;
//                }
//            }
//        }
//        for (int j = 0; j <= msbIndex; j++)
//        {
//            bytes[curByte++] = unchecked((byte)highDword);
//            highDword >>= 8;
//        }
//        if (needExtraByte)
//        {
//            bytes[bytes.Length - 1] = highByte;
//        }
//        return bytes;
//    }

//    /// <summary>
//    /// Return the value of this BigInteger as a little-endian twos-complement
//    /// uint array, using the fewest number of uints possible. If the value is zero,
//    /// return an array of one uint whose element is 0.
//    /// </summary>
//    /// <returns></returns>
//    private uint[] ToUInt32Array()
//    {
//        if (_bits == null && _sign == 0)
//            return new uint[] { 0 };

//        uint[] dwords;
//        uint highDWord;

//        if (_bits == null)
//        {
//            dwords = new uint[] { unchecked((uint)_sign) };
//            highDWord = (_sign < 0) ? uint.MaxValue : 0;
//        }
//        else if (_sign == -1)
//        {
//            dwords = (uint[])_bits.Clone();
//            NumericsHelpers.DangerousMakeTwosComplement(dwords);  // Mutates dwords
//            highDWord = uint.MaxValue;
//        }
//        else
//        {
//            dwords = _bits;
//            highDWord = 0;
//        }

//        // Find highest significant byte
//        int msb;
//        for (msb = dwords.Length - 1; msb > 0; msb--)
//        {
//            if (dwords[msb] != highDWord) break;
//        }
//        // Ensure high bit is 0 if positive, 1 if negative
//        bool needExtraByte = (dwords[msb] & 0x80000000) != (highDWord & 0x80000000);

//        uint[] trimmed = new uint[msb + 1 + (needExtraByte ? 1 : 0)];
//        Array.Copy(dwords, 0, trimmed, 0, msb + 1);

//        if (needExtraByte) trimmed[trimmed.Length - 1] = highDWord;
//        return trimmed;
//    }

//    public override string ToString()
//    {
//        return BigNumber.FormatBigInteger(this, null, NumberFormatInfo.CurrentInfo);
//    }

//    public string ToString(IFormatProvider provider)
//    {
//        return BigNumber.FormatBigInteger(this, null, NumberFormatInfo.GetInstance(provider));
//    }

//    public string ToString(string format)
//    {
//        return BigNumber.FormatBigInteger(this, format, NumberFormatInfo.CurrentInfo);
//    }

//    public string ToString(string format, IFormatProvider provider)
//    {
//        return BigNumber.FormatBigInteger(this, format, NumberFormatInfo.GetInstance(provider));
//    }

//    private static BigInteger Add(uint[] leftBits, int leftSign, uint[] rightBits, int rightSign)
//    {
//        bool trivialLeft = leftBits == null;
//        bool trivialRight = rightBits == null;

//        if (trivialLeft && trivialRight)
//        {
//            return (long)leftSign + rightSign;
//        }

//        if (trivialLeft)
//        {
//            uint[] bits = BigIntegerCalculator.Add(rightBits, NumericsHelpers.Abs(leftSign));
//            return new BigInteger(bits, leftSign < 0);
//        }

//        if (trivialRight)
//        {
//            uint[] bits = BigIntegerCalculator.Add(leftBits, NumericsHelpers.Abs(rightSign));
//            return new BigInteger(bits, leftSign < 0);
//        }

//        if (leftBits.Length < rightBits.Length)
//        {
//            uint[] bits = BigIntegerCalculator.Add(rightBits, leftBits);
//            return new BigInteger(bits, leftSign < 0);
//        }
//        else
//        {
//            uint[] bits = BigIntegerCalculator.Add(leftBits, rightBits);
//            return new BigInteger(bits, leftSign < 0);
//        }
//    }

//    public static BigInteger operator -(BigInteger left, BigInteger right)
//    {
//        left.AssertValid();
//        right.AssertValid();

//        if (left._sign < 0 != right._sign < 0)
//            return Add(left._bits, left._sign, right._bits, -1 * right._sign);
//        return Subtract(left._bits, left._sign, right._bits, right._sign);
//    }

//    private static BigInteger Subtract(uint[] leftBits, int leftSign, uint[] rightBits, int rightSign)
//    {
//        bool trivialLeft = leftBits == null;
//        bool trivialRight = rightBits == null;

//        if (trivialLeft && trivialRight)
//        {
//            return (long)leftSign - rightSign;
//        }

//        if (trivialLeft)
//        {
//            uint[] bits = BigIntegerCalculator.Subtract(rightBits, NumericsHelpers.Abs(leftSign));
//            return new BigInteger(bits, leftSign >= 0);
//        }

//        if (trivialRight)
//        {
//            uint[] bits = BigIntegerCalculator.Subtract(leftBits, NumericsHelpers.Abs(rightSign));
//            return new BigInteger(bits, leftSign < 0);
//        }

//        if (BigIntegerCalculator.Compare(leftBits, rightBits) < 0)
//        {
//            uint[] bits = BigIntegerCalculator.Subtract(rightBits, leftBits);
//            return new BigInteger(bits, leftSign >= 0);
//        }
//        else
//        {
//            uint[] bits = BigIntegerCalculator.Subtract(leftBits, rightBits);
//            return new BigInteger(bits, leftSign < 0);
//        }
//    }

//    public static implicit operator BigInteger(byte value)
//    {
//        return new BigInteger(value);
//    }

//    [CLSCompliant(false)]
//    public static implicit operator BigInteger(sbyte value)
//    {
//        return new BigInteger(value);
//    }

//    public static implicit operator BigInteger(short value)
//    {
//        return new BigInteger(value);
//    }

//    [CLSCompliant(false)]
//    public static implicit operator BigInteger(ushort value)
//    {
//        return new BigInteger(value);
//    }

//    public static implicit operator BigInteger(int value)
//    {
//        return new BigInteger(value);
//    }

//    [CLSCompliant(false)]
//    public static implicit operator BigInteger(uint value)
//    {
//        return new BigInteger(value);
//    }

//    public static implicit operator BigInteger(long value)
//    {
//        return new BigInteger(value);
//    }

//    [CLSCompliant(false)]
//    public static implicit operator BigInteger(ulong value)
//    {
//        return new BigInteger(value);
//    }

//    public static explicit operator BigInteger(float value)
//    {
//        return new BigInteger(value);
//    }

//    public static explicit operator BigInteger(double value)
//    {
//        return new BigInteger(value);
//    }

//    public static explicit operator BigInteger(decimal value)
//    {
//        return new BigInteger(value);
//    }

//    public static explicit operator byte(BigInteger value)
//    {
//        return checked((byte)((int)value));
//    }

//    [CLSCompliant(false)]
//    public static explicit operator sbyte(BigInteger value)
//    {
//        return checked((sbyte)((int)value));
//    }

//    public static explicit operator short(BigInteger value)
//    {
//        return checked((short)((int)value));
//    }

//    [CLSCompliant(false)]
//    public static explicit operator ushort(BigInteger value)
//    {
//        return checked((ushort)((int)value));
//    }

//    public static explicit operator int(BigInteger value)
//    {
//        value.AssertValid();
//        if (value._bits == null)
//        {
//            return value._sign;  // Value packed into int32 sign
//        }
//        if (value._bits.Length > 1)
//        {
//            // More than 32 bits
//            throw new OverflowException(SR.Overflow_Int32);
//        }
//        if (value._sign > 0)
//        {
//            return checked((int)value._bits[0]);
//        }
//        if (value._bits[0] > kuMaskHighBit)
//        {
//            // Value > Int32.MinValue
//            throw new OverflowException(SR.Overflow_Int32);
//        }
//        return unchecked(-(int)value._bits[0]);
//    }

//    [CLSCompliant(false)]
//    public static explicit operator uint(BigInteger value)
//    {
//        value.AssertValid();
//        if (value._bits == null)
//        {
//            return checked((uint)value._sign);
//        }
//        else if (value._bits.Length > 1 || value._sign < 0)
//        {
//            throw new OverflowException(SR.Overflow_UInt32);
//        }
//        else
//        {
//            return value._bits[0];
//        }
//    }

//    public static explicit operator long(BigInteger value)
//    {
//        value.AssertValid();
//        if (value._bits == null)
//        {
//            return value._sign;
//        }

//        int len = value._bits.Length;
//        if (len > 2)
//        {
//            throw new OverflowException(SR.Overflow_Int64);
//        }

//        ulong uu;
//        if (len > 1)
//        {
//            uu = NumericsHelpers.MakeUlong(value._bits[1], value._bits[0]);
//        }
//        else
//        {
//            uu = value._bits[0];
//        }

//        long ll = value._sign > 0 ? unchecked((long)uu) : unchecked(-(long)uu);
//        if ((ll > 0 && value._sign > 0) || (ll < 0 && value._sign < 0))
//        {
//            // Signs match, no overflow
//            return ll;
//        }
//        throw new OverflowException(SR.Overflow_Int64);
//    }

//    [CLSCompliant(false)]
//    public static explicit operator ulong(BigInteger value)
//    {
//        value.AssertValid();
//        if (value._bits == null)
//        {
//            return checked((ulong)value._sign);
//        }

//        int len = value._bits.Length;
//        if (len > 2 || value._sign < 0)
//        {
//            throw new OverflowException(SR.Overflow_UInt64);
//        }

//        if (len > 1)
//        {
//            return NumericsHelpers.MakeUlong(value._bits[1], value._bits[0]);
//        }
//        return value._bits[0];
//    }

//    public static explicit operator float(BigInteger value)
//    {
//        return (float)((double)value);
//    }

//    public static explicit operator double(BigInteger value)
//    {
//        value.AssertValid();

//        int sign = value._sign;
//        uint[] bits = value._bits;

//        if (bits == null)
//            return sign;

//        int length = bits.Length;

//        // The maximum exponent for doubles is 1023, which corresponds to a uint bit length of 32.
//        // All BigIntegers with bits[] longer than 32 evaluate to Double.Infinity (or NegativeInfinity).
//        // Cases where the exponent is between 1024 and 1035 are handled in NumericsHelpers.GetDoubleFromParts.
//        const int InfinityLength = 1024 / kcbitUint;

//        if (length > InfinityLength)
//        {
//            if (sign == 1)
//                return double.PositiveInfinity;
//            else
//                return double.NegativeInfinity;
//        }

//        ulong h = bits[length - 1];
//        ulong m = length > 1 ? bits[length - 2] : 0;
//        ulong l = length > 2 ? bits[length - 3] : 0;

//        int z = NumericsHelpers.CbitHighZero((uint)h);

//        int exp = (length - 2) * 32 - z;
//        ulong man = (h << 32 + z) | (m << z) | (l >> 32 - z);

//        return NumericsHelpers.GetDoubleFromParts(sign, exp, man);
//    }

//    public static explicit operator decimal(BigInteger value)
//    {
//        value.AssertValid();
//        if (value._bits == null)
//            return value._sign;

//        int length = value._bits.Length;
//        if (length > 3) throw new OverflowException(SR.Overflow_Decimal);

//        int lo = 0, mi = 0, hi = 0;

//        unchecked
//        {
//            if (length > 2) hi = (int)value._bits[2];
//            if (length > 1) mi = (int)value._bits[1];
//            if (length > 0) lo = (int)value._bits[0];
//        }

//        return new decimal(lo, mi, hi, value._sign < 0, 0);
//    }

//    public static BigInteger operator &(BigInteger left, BigInteger right)
//    {
//        if (left.IsZero || right.IsZero)
//        {
//            return Zero;
//        }

//        if (left._bits == null && right._bits == null)
//        {
//            return left._sign & right._sign;
//        }

//        uint[] x = left.ToUInt32Array();
//        uint[] y = right.ToUInt32Array();
//        uint[] z = new uint[Math.Max(x.Length, y.Length)];
//        uint xExtend = (left._sign < 0) ? uint.MaxValue : 0;
//        uint yExtend = (right._sign < 0) ? uint.MaxValue : 0;

//        for (int i = 0; i < z.Length; i++)
//        {
//            uint xu = (i < x.Length) ? x[i] : xExtend;
//            uint yu = (i < y.Length) ? y[i] : yExtend;
//            z[i] = xu & yu;
//        }
//        return new BigInteger(z);
//    }

//    public static BigInteger operator |(BigInteger left, BigInteger right)
//    {
//        if (left.IsZero)
//            return right;
//        if (right.IsZero)
//            return left;

//        if (left._bits == null && right._bits == null)
//        {
//            return left._sign | right._sign;
//        }

//        uint[] x = left.ToUInt32Array();
//        uint[] y = right.ToUInt32Array();
//        uint[] z = new uint[Math.Max(x.Length, y.Length)];
//        uint xExtend = (left._sign < 0) ? uint.MaxValue : 0;
//        uint yExtend = (right._sign < 0) ? uint.MaxValue : 0;

//        for (int i = 0; i < z.Length; i++)
//        {
//            uint xu = (i < x.Length) ? x[i] : xExtend;
//            uint yu = (i < y.Length) ? y[i] : yExtend;
//            z[i] = xu | yu;
//        }
//        return new BigInteger(z);
//    }

//    public static BigInteger operator ^(BigInteger left, BigInteger right)
//    {
//        if (left._bits == null && right._bits == null)
//        {
//            return left._sign ^ right._sign;
//        }

//        uint[] x = left.ToUInt32Array();
//        uint[] y = right.ToUInt32Array();
//        uint[] z = new uint[Math.Max(x.Length, y.Length)];
//        uint xExtend = (left._sign < 0) ? uint.MaxValue : 0;
//        uint yExtend = (right._sign < 0) ? uint.MaxValue : 0;

//        for (int i = 0; i < z.Length; i++)
//        {
//            uint xu = (i < x.Length) ? x[i] : xExtend;
//            uint yu = (i < y.Length) ? y[i] : yExtend;
//            z[i] = xu ^ yu;
//        }

//        return new BigInteger(z);
//    }

//    public static BigInteger operator <<(BigInteger value, int shift)
//    {
//        if (shift == 0) return value;
//        else if (shift == int.MinValue) return ((value >> int.MaxValue) >> 1);
//        else if (shift < 0) return value >> -shift;

//        int digitShift = shift / kcbitUint;
//        int smallShift = shift - (digitShift * kcbitUint);

//        uint[] xd; int xl; bool negx;
//        negx = GetPartsForBitManipulation(ref value, out xd, out xl);

//        int zl = xl + digitShift + 1;
//        uint[] zd = new uint[zl];

//        if (smallShift == 0)
//        {
//            for (int i = 0; i < xl; i++)
//            {
//                zd[i + digitShift] = xd[i];
//            }
//        }
//        else
//        {
//            int carryShift = kcbitUint - smallShift;
//            uint carry = 0;
//            int i;
//            for (i = 0; i < xl; i++)
//            {
//                uint rot = xd[i];
//                zd[i + digitShift] = rot << smallShift | carry;
//                carry = rot >> carryShift;
//            }
//            zd[i + digitShift] = carry;
//        }
//        return new BigInteger(zd, negx);
//    }

//    public static BigInteger operator >>(BigInteger value, int shift)
//    {
//        if (shift == 0) return value;
//        else if (shift == int.MinValue) return ((value << int.MaxValue) << 1);
//        else if (shift < 0) return value << -shift;

//        int digitShift = shift / kcbitUint;
//        int smallShift = shift - (digitShift * kcbitUint);

//        uint[] xd; int xl; bool negx;
//        negx = GetPartsForBitManipulation(ref value, out xd, out xl);

//        if (negx)
//        {
//            if (shift >= (kcbitUint * xl))
//            {
//                return MinusOne;
//            }
//            uint[] temp = new uint[xl];
//            Array.Copy(xd /* sourceArray */, 0 /* sourceIndex */, temp /* destinationArray */, 0 /* destinationIndex */, xl /* length */);  // Make a copy of immutable value._bits
//            xd = temp;
//            NumericsHelpers.DangerousMakeTwosComplement(xd); // Mutates xd
//        }

//        int zl = xl - digitShift;
//        if (zl < 0) zl = 0;
//        uint[] zd = new uint[zl];

//        if (smallShift == 0)
//        {
//            for (int i = xl - 1; i >= digitShift; i--)
//            {
//                zd[i - digitShift] = xd[i];
//            }
//        }
//        else
//        {
//            int carryShift = kcbitUint - smallShift;
//            uint carry = 0;
//            for (int i = xl - 1; i >= digitShift; i--)
//            {
//                uint rot = xd[i];
//                if (negx && i == xl - 1)
//                    // Sign-extend the first shift for negative ints then let the carry propagate
//                    zd[i - digitShift] = (rot >> smallShift) | (0xFFFFFFFF << carryShift);
//                else
//                    zd[i - digitShift] = (rot >> smallShift) | carry;
//                carry = rot << carryShift;
//            }
//        }
//        if (negx)
//        {
//            NumericsHelpers.DangerousMakeTwosComplement(zd); // Mutates zd
//        }
//        return new BigInteger(zd, negx);
//    }

//    public static BigInteger operator ~(BigInteger value)
//    {
//        return -(value + One);
//    }

//    public static BigInteger operator -(BigInteger value)
//    {
//        value.AssertValid();
//        return new BigInteger(-value._sign, value._bits);
//    }

//    public static BigInteger operator +(BigInteger value)
//    {
//        value.AssertValid();
//        return value;
//    }

//    public static BigInteger operator ++(BigInteger value)
//    {
//        return value + One;
//    }

//    public static BigInteger operator --(BigInteger value)
//    {
//        return value - One;
//    }

//    public static BigInteger operator +(BigInteger left, BigInteger right)
//    {
//        left.AssertValid();
//        right.AssertValid();

//        if (left._sign < 0 != right._sign < 0)
//            return Subtract(left._bits, left._sign, right._bits, -1 * right._sign);
//        return Add(left._bits, left._sign, right._bits, right._sign);
//    }

//    public static BigInteger operator *(BigInteger left, BigInteger right)
//    {
//        left.AssertValid();
//        right.AssertValid();

//        bool trivialLeft = left._bits == null;
//        bool trivialRight = right._bits == null;

//        if (trivialLeft && trivialRight)
//        {
//            return (long)left._sign * right._sign;
//        }

//        if (trivialLeft)
//        {
//            uint[] bits = BigIntegerCalculator.Multiply(right._bits, NumericsHelpers.Abs(left._sign));
//            return new BigInteger(bits, (left._sign < 0) ^ (right._sign < 0));
//        }

//        if (trivialRight)
//        {
//            uint[] bits = BigIntegerCalculator.Multiply(left._bits, NumericsHelpers.Abs(right._sign));
//            return new BigInteger(bits, (left._sign < 0) ^ (right._sign < 0));
//        }

//        if (left._bits == right._bits)
//        {
//            uint[] bits = BigIntegerCalculator.Square(left._bits);
//            return new BigInteger(bits, (left._sign < 0) ^ (right._sign < 0));
//        }

//        if (left._bits.Length < right._bits.Length)
//        {
//            uint[] bits = BigIntegerCalculator.Multiply(right._bits, left._bits);
//            return new BigInteger(bits, (left._sign < 0) ^ (right._sign < 0));
//        }
//        else
//        {
//            uint[] bits = BigIntegerCalculator.Multiply(left._bits, right._bits);
//            return new BigInteger(bits, (left._sign < 0) ^ (right._sign < 0));
//        }
//    }

//    public static BigInteger operator /(BigInteger dividend, BigInteger divisor)
//    {
//        dividend.AssertValid();
//        divisor.AssertValid();

//        bool trivialDividend = dividend._bits == null;
//        bool trivialDivisor = divisor._bits == null;

//        if (trivialDividend && trivialDivisor)
//        {
//            return dividend._sign / divisor._sign;
//        }

//        if (trivialDividend)
//        {
//            // The divisor is non-trivial
//            // and therefore the bigger one
//            return s_bnZeroInt;
//        }

//        if (trivialDivisor)
//        {
//            uint[] bits = BigIntegerCalculator.Divide(dividend._bits, NumericsHelpers.Abs(divisor._sign));
//            return new BigInteger(bits, (dividend._sign < 0) ^ (divisor._sign < 0));
//        }

//        if (dividend._bits.Length < divisor._bits.Length)
//        {
//            return s_bnZeroInt;
//        }
//        else
//        {
//            uint[] bits = BigIntegerCalculator.Divide(dividend._bits, divisor._bits);
//            return new BigInteger(bits, (dividend._sign < 0) ^ (divisor._sign < 0));
//        }
//    }

//    public static BigInteger operator %(BigInteger dividend, BigInteger divisor)
//    {
//        dividend.AssertValid();
//        divisor.AssertValid();

//        bool trivialDividend = dividend._bits == null;
//        bool trivialDivisor = divisor._bits == null;

//        if (trivialDividend && trivialDivisor)
//        {
//            return dividend._sign % divisor._sign;
//        }

//        if (trivialDividend)
//        {
//            // The divisor is non-trivial
//            // and therefore the bigger one
//            return dividend;
//        }

//        if (trivialDivisor)
//        {
//            uint remainder = BigIntegerCalculator.Remainder(dividend._bits, NumericsHelpers.Abs(divisor._sign));
//            return dividend._sign < 0 ? -1 * remainder : remainder;
//        }

//        if (dividend._bits.Length < divisor._bits.Length)
//        {
//            return dividend;
//        }
//        uint[] bits = BigIntegerCalculator.Remainder(dividend._bits, divisor._bits);
//        return new BigInteger(bits, dividend._sign < 0);
//    }

//    public static bool operator <(BigInteger left, BigInteger right)
//    {
//        return left.CompareTo(right) < 0;
//    }

//    public static bool operator <=(BigInteger left, BigInteger right)
//    {
//        return left.CompareTo(right) <= 0;
//    }

//    public static bool operator >(BigInteger left, BigInteger right)
//    {
//        return left.CompareTo(right) > 0;
//    }
//    public static bool operator >=(BigInteger left, BigInteger right)
//    {
//        return left.CompareTo(right) >= 0;
//    }

//    public static bool operator ==(BigInteger left, BigInteger right)
//    {
//        return left.Equals(right);
//    }

//    public static bool operator !=(BigInteger left, BigInteger right)
//    {
//        return !left.Equals(right);
//    }

//    public static bool operator <(BigInteger left, long right)
//    {
//        return left.CompareTo(right) < 0;
//    }

//    public static bool operator <=(BigInteger left, long right)
//    {
//        return left.CompareTo(right) <= 0;
//    }

//    public static bool operator >(BigInteger left, long right)
//    {
//        return left.CompareTo(right) > 0;
//    }

//    public static bool operator >=(BigInteger left, long right)
//    {
//        return left.CompareTo(right) >= 0;
//    }

//    public static bool operator ==(BigInteger left, long right)
//    {
//        return left.Equals(right);
//    }

//    public static bool operator !=(BigInteger left, long right)
//    {
//        return !left.Equals(right);
//    }

//    public static bool operator <(long left, BigInteger right)
//    {
//        return right.CompareTo(left) > 0;
//    }

//    public static bool operator <=(long left, BigInteger right)
//    {
//        return right.CompareTo(left) >= 0;
//    }

//    public static bool operator >(long left, BigInteger right)
//    {
//        return right.CompareTo(left) < 0;
//    }

//    public static bool operator >=(long left, BigInteger right)
//    {
//        return right.CompareTo(left) <= 0;
//    }

//    public static bool operator ==(long left, BigInteger right)
//    {
//        return right.Equals(left);
//    }

//    public static bool operator !=(long left, BigInteger right)
//    {
//        return !right.Equals(left);
//    }

//    [CLSCompliant(false)]
//    public static bool operator <(BigInteger left, ulong right)
//    {
//        return left.CompareTo(right) < 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator <=(BigInteger left, ulong right)
//    {
//        return left.CompareTo(right) <= 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator >(BigInteger left, ulong right)
//    {
//        return left.CompareTo(right) > 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator >=(BigInteger left, ulong right)
//    {
//        return left.CompareTo(right) >= 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator ==(BigInteger left, ulong right)
//    {
//        return left.Equals(right);
//    }

//    [CLSCompliant(false)]
//    public static bool operator !=(BigInteger left, ulong right)
//    {
//        return !left.Equals(right);
//    }

//    [CLSCompliant(false)]
//    public static bool operator <(ulong left, BigInteger right)
//    {
//        return right.CompareTo(left) > 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator <=(ulong left, BigInteger right)
//    {
//        return right.CompareTo(left) >= 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator >(ulong left, BigInteger right)
//    {
//        return right.CompareTo(left) < 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator >=(ulong left, BigInteger right)
//    {
//        return right.CompareTo(left) <= 0;
//    }

//    [CLSCompliant(false)]
//    public static bool operator ==(ulong left, BigInteger right)
//    {
//        return right.Equals(left);
//    }

//    [CLSCompliant(false)]
//    public static bool operator !=(ulong left, BigInteger right)
//    {
//        return !right.Equals(left);
//    }

//    /// <summary>
//    /// Encapsulate the logic of normalizing the "small" and "large" forms of BigInteger
//    /// into the "large" form so that Bit Manipulation algorithms can be simplified. 
//    /// </summary>
//    /// <param name="x"></param>
//    /// <param name="xd">
//    /// The UInt32 array containing the entire big integer in "large" (denormalized) form.
//    /// E.g., the number one (1) and negative one (-1) are both stored as 0x00000001
//    //  BigInteger values Int32.MinValue < x <= Int32.MaxValue are converted to this
//    //  format for convenience.
//    /// </param>
//    /// <param name="xl">The length of xd.</param>
//    /// <returns>True for negative numbers.</returns>
//    private static bool GetPartsForBitManipulation(ref BigInteger x, out uint[] xd, out int xl)
//    {
//        if (x._bits == null)
//        {
//            if (x._sign < 0)
//            {
//                xd = new uint[] { (uint)-x._sign };
//            }
//            else
//            {
//                xd = new uint[] { (uint)x._sign };
//            }
//        }
//        else
//        {
//            xd = x._bits;
//        }
//        xl = (x._bits == null ? 1 : x._bits.Length);
//        return x._sign < 0;
//    }

//    internal static int GetDiffLength(uint[] rgu1, uint[] rgu2, int cu)
//    {
//        for (int iv = cu; --iv >= 0;)
//        {
//            if (rgu1[iv] != rgu2[iv])
//                return iv + 1;
//        }
//        return 0;
//    }

//    [Conditional("DEBUG")]
//    private void AssertValid()
//    {
//        if (_bits != null)
//        {
//            // _sign must be +1 or -1 when _bits is non-null
//            Debug.Assert(_sign == 1 || _sign == -1);
//            // _bits must contain at least 1 element or be null
//            Debug.Assert(_bits.Length > 0);
//            // Wasted space: _bits[0] could have been packed into _sign
//            Debug.Assert(_bits.Length > 1 || _bits[0] >= kuMaskHighBit);
//            // Wasted space: leading zeros could have been truncated
//            Debug.Assert(_bits[_bits.Length - 1] != 0);
//        }
//        else
//        {
//            // Int32.MinValue should not be stored in the _sign field
//            Debug.Assert(_sign > int.MinValue);
//        }
//    }
//}

//internal struct DoubleUlong
//{
//    public double dbl;
//    public ulong uu;
//}

//internal static class NumericsHelpers
//{
//    private const int kcbitUint = 32;

//    public static void GetDoubleParts(double dbl, out int sign, out int exp, out ulong man, out bool fFinite)
//    {
//        DoubleUlong du;
//        du.uu = 0;
//        du.dbl = dbl;

//        sign = 1 - ((int)(du.uu >> 62) & 2);
//        man = du.uu & 0x000FFFFFFFFFFFFF;
//        exp = (int)(du.uu >> 52) & 0x7FF;
//        if (exp == 0)
//        {
//            // Denormalized number.
//            fFinite = true;
//            if (man != 0)
//                exp = -1074;
//        }
//        else if (exp == 0x7FF)
//        {
//            // NaN or Infinite.
//            fFinite = false;
//            exp = int.MaxValue;
//        }
//        else
//        {
//            fFinite = true;
//            man |= 0x0010000000000000;
//            exp -= 1075;
//        }
//    }

//    public static double GetDoubleFromParts(int sign, int exp, ulong man)
//    {
//        DoubleUlong du;
//        du.dbl = 0;

//        if (man == 0)
//            du.uu = 0;
//        else
//        {
//            // Normalize so that 0x0010 0000 0000 0000 is the highest bit set.
//            int cbitShift = CbitHighZero(man) - 11;
//            if (cbitShift < 0)
//                man >>= -cbitShift;
//            else
//                man <<= cbitShift;
//            exp -= cbitShift;
//            Debug.Assert((man & 0xFFF0000000000000) == 0x0010000000000000);

//            // Move the point to just behind the leading 1: 0x001.0 0000 0000 0000
//            // (52 bits) and skew the exponent (by 0x3FF == 1023).
//            exp += 1075;

//            if (exp >= 0x7FF)
//            {
//                // Infinity.
//                du.uu = 0x7FF0000000000000;
//            }
//            else if (exp <= 0)
//            {
//                // Denormalized.
//                exp--;
//                if (exp < -52)
//                {
//                    // Underflow to zero.
//                    du.uu = 0;
//                }
//                else
//                {
//                    du.uu = man >> -exp;
//                    Debug.Assert(du.uu != 0);
//                }
//            }
//            else
//            {
//                // Mask off the implicit high bit.
//                du.uu = (man & 0x000FFFFFFFFFFFFF) | ((ulong)exp << 52);
//            }
//        }

//        if (sign < 0)
//            du.uu |= 0x8000000000000000;

//        return du.dbl;
//    }

//    // Do an in-place two's complement. "Dangerous" because it causes
//    // a mutation and needs to be used with care for immutable types.
//    public static void DangerousMakeTwosComplement(uint[] d)
//    {
//        if (d != null && d.Length > 0)
//        {
//            d[0] = unchecked(~d[0] + 1);

//            int i = 1;
//            // first do complement and +1 as long as carry is needed
//            for (; d[i - 1] == 0 && i < d.Length; i++)
//            {
//                d[i] = unchecked(~d[i] + 1);
//            }
//            // now ones complement is sufficient
//            for (; i < d.Length; i++)
//            {
//                d[i] = ~d[i];
//            }
//        }
//    }

//    public static ulong MakeUlong(uint uHi, uint uLo)
//    {
//        return ((ulong)uHi << kcbitUint) | uLo;
//    }

//    public static uint Abs(int a)
//    {
//        unchecked
//        {
//            uint mask = (uint)(a >> 31);
//            return ((uint)a ^ mask) - mask;
//        }
//    }

//    public static uint CombineHash(uint u1, uint u2)
//    {
//        return ((u1 << 7) | (u1 >> 25)) ^ u2;
//    }

//    public static int CombineHash(int n1, int n2)
//    {
//        return unchecked((int)CombineHash((uint)n1, (uint)n2));
//    }

//    public static int CbitHighZero(uint u)
//    {
//        if (u == 0)
//            return 32;

//        int cbit = 0;
//        if ((u & 0xFFFF0000) == 0)
//        {
//            cbit += 16;
//            u <<= 16;
//        }
//        if ((u & 0xFF000000) == 0)
//        {
//            cbit += 8;
//            u <<= 8;
//        }
//        if ((u & 0xF0000000) == 0)
//        {
//            cbit += 4;
//            u <<= 4;
//        }
//        if ((u & 0xC0000000) == 0)
//        {
//            cbit += 2;
//            u <<= 2;
//        }
//        if ((u & 0x80000000) == 0)
//            cbit += 1;
//        return cbit;
//    }

//    public static int CbitHighZero(ulong uu)
//    {
//        if ((uu & 0xFFFFFFFF00000000) == 0)
//            return 32 + CbitHighZero((uint)uu);
//        return CbitHighZero((uint)(uu >> 32));
//    }
//}

//internal static partial class BigIntegerCalculator
//{
//        public static unsafe uint[] Square(uint[] value)
//        {
//            Debug.Assert(value != null);

//            // Switching to unsafe pointers helps sparing
//            // some nasty index calculations...

//            uint[] bits = new uint[value.Length + value.Length];

//            fixed (uint* v = value, b = bits)
//            {
//                Square(v, value.Length,
//                       b, bits.Length);
//            }

//            return bits;
//        }

//        // Mutable for unit testing...
//        private static int SquareThreshold = 32;
//        private static int AllocationThreshold = 256;

//        private static unsafe void Square(uint* value, int valueLength,
//                                          uint* bits, int bitsLength)
//        {
//            Debug.Assert(valueLength >= 0);
//            Debug.Assert(bitsLength == valueLength + valueLength);

//            // Executes different algorithms for computing z = a * a
//            // based on the actual length of a. If a is "small" enough
//            // we stick to the classic "grammar-school" method; for the
//            // rest we switch to implementations with less complexity
//            // albeit more overhead (which needs to pay off!).

//            // NOTE: useful thresholds needs some "empirical" testing,
//            // which are smaller in DEBUG mode for testing purpose.

//            if (valueLength < SquareThreshold)
//            {
//                // Squares the bits using the "grammar-school" method.
//                // Envisioning the "rhombus" of a pen-and-paper calculation
//                // we see that computing z_i+j += a_j * a_i can be optimized
//                // since a_j * a_i = a_i * a_j (we're squaring after all!).
//                // Thus, we directly get z_i+j += 2 * a_j * a_i + c.

//                // ATTENTION: an ordinary multiplication is safe, because
//                // z_i+j + a_j * a_i + c <= 2(2^32 - 1) + (2^32 - 1)^2 =
//                // = 2^64 - 1 (which perfectly matches with ulong!). But
//                // here we would need an UInt65... Hence, we split these
//                // operation and do some extra shifts.

//                for (int i = 0; i < valueLength; i++)
//                {
//                    ulong carry = 0UL;
//                    for (int j = 0; j < i; j++)
//                    {
//                        ulong digit1 = bits[i + j] + carry;
//                        ulong digit2 = (ulong)value[j] * value[i];
//                        bits[i + j] = unchecked((uint)(digit1 + (digit2 << 1)));
//                        carry = (digit2 + (digit1 >> 1)) >> 31;
//                    }
//                    ulong digits = (ulong)value[i] * value[i] + carry;
//                    bits[i + i] = unchecked((uint)digits);
//                    bits[i + i + 1] = (uint)(digits >> 32);
//                }
//            }
//            else
//            {
//                // Based on the Toom-Cook multiplication we split value
//                // into two smaller values, doing recursive squaring.
//                // The special form of this multiplication, where we
//                // split both operands into two operands, is also known
//                // as the Karatsuba algorithm...

//                // https://en.wikipedia.org/wiki/Toom-Cook_multiplication
//                // https://en.wikipedia.org/wiki/Karatsuba_algorithm

//                // Say we want to compute z = a * a ...

//                // ... we need to determine our new length (just the half)
//                int n = valueLength >> 1;
//                int n2 = n << 1;

//                // ... split value like a = (a_1 << n) + a_0
//                uint* valueLow = value;
//                int valueLowLength = n;
//                uint* valueHigh = value + n;
//                int valueHighLength = valueLength - n;

//                // ... prepare our result array (to reuse its memory)
//                uint* bitsLow = bits;
//                int bitsLowLength = n2;
//                uint* bitsHigh = bits + n2;
//                int bitsHighLength = bitsLength - n2;

//                // ... compute z_0 = a_0 * a_0 (squaring again!)
//                Square(valueLow, valueLowLength,
//                       bitsLow, bitsLowLength);

//                // ... compute z_2 = a_1 * a_1 (squaring again!)
//                Square(valueHigh, valueHighLength,
//                       bitsHigh, bitsHighLength);

//                int foldLength = valueHighLength + 1;
//                int coreLength = foldLength + foldLength;

//                if (coreLength < AllocationThreshold)
//                {
//                    uint* fold = stackalloc uint[foldLength];
//                    uint* core = stackalloc uint[coreLength];

//                    // ... compute z_a = a_1 + a_0 (call it fold...)
//                    Add(valueHigh, valueHighLength,
//                        valueLow, valueLowLength,
//                        fold, foldLength);

//                    // ... compute z_1 = z_a * z_a - z_0 - z_2
//                    Square(fold, foldLength,
//                           core, coreLength);
//                    SubtractCore(bitsHigh, bitsHighLength,
//                                 bitsLow, bitsLowLength,
//                                 core, coreLength);

//                    // ... and finally merge the result! :-)
//                    AddSelf(bits + n, bitsLength - n, core, coreLength);
//                }
//                else
//                {
//                    fixed (uint* fold = new uint[foldLength],
//                                 core = new uint[coreLength])
//                    {
//                        // ... compute z_a = a_1 + a_0 (call it fold...)
//                        Add(valueHigh, valueHighLength,
//                            valueLow, valueLowLength,
//                            fold, foldLength);

//                        // ... compute z_1 = z_a * z_a - z_0 - z_2
//                        Square(fold, foldLength,
//                               core, coreLength);
//                        SubtractCore(bitsHigh, bitsHighLength,
//                                     bitsLow, bitsLowLength,
//                                     core, coreLength);

//                        // ... and finally merge the result! :-)
//                        AddSelf(bits + n, bitsLength - n, core, coreLength);
//                    }
//                }
//            }
//        }

//        public static uint[] Multiply(uint[] left, uint right)
//        {
//            Debug.Assert(left != null);

//            // Executes the multiplication for one big and one 32-bit integer.
//            // Since every step holds the already slightly familiar equation
//            // a_i * b + c <= 2^32 - 1 + (2^32 - 1)^2 < 2^64 - 1,
//            // we are safe regarding to overflows.

//            int i = 0;
//            ulong carry = 0UL;
//            uint[] bits = new uint[left.Length + 1];

//            for (; i < left.Length; i++)
//            {
//                ulong digits = (ulong)left[i] * right + carry;
//                bits[i] = unchecked((uint)digits);
//                carry = digits >> 32;
//            }
//            bits[i] = (uint)carry;

//            return bits;
//        }

//        public static unsafe uint[] Multiply(uint[] left, uint[] right)
//        {
//            Debug.Assert(left != null);
//            Debug.Assert(right != null);
//            Debug.Assert(left.Length >= right.Length);

//            // Switching to unsafe pointers helps sparing
//            // some nasty index calculations...

//            uint[] bits = new uint[left.Length + right.Length];

//            fixed (uint* l = left, r = right, b = bits)
//            {
//                Multiply(l, left.Length,
//                         r, right.Length,
//                         b, bits.Length);
//            }

//            return bits;
//        }

//        // Mutable for unit testing...
//        private static int MultiplyThreshold = 32;

//        private static unsafe void Multiply(uint* left, int leftLength,
//                                            uint* right, int rightLength,
//                                            uint* bits, int bitsLength)
//        {
//            Debug.Assert(leftLength >= 0);
//            Debug.Assert(rightLength >= 0);
//            Debug.Assert(leftLength >= rightLength);
//            Debug.Assert(bitsLength == leftLength + rightLength);

//            // Executes different algorithms for computing z = a * b
//            // based on the actual length of b. If b is "small" enough
//            // we stick to the classic "grammar-school" method; for the
//            // rest we switch to implementations with less complexity
//            // albeit more overhead (which needs to pay off!).

//            // NOTE: useful thresholds needs some "empirical" testing,
//            // which are smaller in DEBUG mode for testing purpose.

//            if (rightLength < MultiplyThreshold)
//            {
//                // Multiplies the bits using the "grammar-school" method.
//                // Envisioning the "rhombus" of a pen-and-paper calculation
//                // should help getting the idea of these two loops...
//                // The inner multiplication operations are safe, because
//                // z_i+j + a_j * b_i + c <= 2(2^32 - 1) + (2^32 - 1)^2 =
//                // = 2^64 - 1 (which perfectly matches with ulong!).

//                for (int i = 0; i < rightLength; i++)
//                {
//                    ulong carry = 0UL;
//                    for (int j = 0; j < leftLength; j++)
//                    {
//                        ulong digits = bits[i + j] + carry
//                            + (ulong)left[j] * right[i];
//                        bits[i + j] = unchecked((uint)digits);
//                        carry = digits >> 32;
//                    }
//                    bits[i + leftLength] = (uint)carry;
//                }
//            }
//            else
//            {
//                // Based on the Toom-Cook multiplication we split left/right
//                // into two smaller values, doing recursive multiplication.
//                // The special form of this multiplication, where we
//                // split both operands into two operands, is also known
//                // as the Karatsuba algorithm...

//                // https://en.wikipedia.org/wiki/Toom-Cook_multiplication
//                // https://en.wikipedia.org/wiki/Karatsuba_algorithm

//                // Say we want to compute z = a * b ...

//                // ... we need to determine our new length (just the half)
//                int n = rightLength >> 1;
//                int n2 = n << 1;

//                // ... split left like a = (a_1 << n) + a_0
//                uint* leftLow = left;
//                int leftLowLength = n;
//                uint* leftHigh = left + n;
//                int leftHighLength = leftLength - n;

//                // ... split right like b = (b_1 << n) + b_0
//                uint* rightLow = right;
//                int rightLowLength = n;
//                uint* rightHigh = right + n;
//                int rightHighLength = rightLength - n;

//                // ... prepare our result array (to reuse its memory)
//                uint* bitsLow = bits;
//                int bitsLowLength = n2;
//                uint* bitsHigh = bits + n2;
//                int bitsHighLength = bitsLength - n2;

//                // ... compute z_0 = a_0 * b_0 (multiply again)
//                Multiply(leftLow, leftLowLength,
//                         rightLow, rightLowLength,
//                         bitsLow, bitsLowLength);

//                // ... compute z_2 = a_1 * b_1 (multiply again)
//                Multiply(leftHigh, leftHighLength,
//                         rightHigh, rightHighLength,
//                         bitsHigh, bitsHighLength);

//                int leftFoldLength = leftHighLength + 1;
//                int rightFoldLength = rightHighLength + 1;
//                int coreLength = leftFoldLength + rightFoldLength;

//                if (coreLength < AllocationThreshold)
//                {
//                    uint* leftFold = stackalloc uint[leftFoldLength];
//                    uint* rightFold = stackalloc uint[rightFoldLength];
//                    uint* core = stackalloc uint[coreLength];

//                    // ... compute z_a = a_1 + a_0 (call it fold...)
//                    Add(leftHigh, leftHighLength,
//                        leftLow, leftLowLength,
//                        leftFold, leftFoldLength);

//                    // ... compute z_b = b_1 + b_0 (call it fold...)
//                    Add(rightHigh, rightHighLength,
//                        rightLow, rightLowLength,
//                        rightFold, rightFoldLength);

//                    // ... compute z_1 = z_a * z_b - z_0 - z_2
//                    Multiply(leftFold, leftFoldLength,
//                             rightFold, rightFoldLength,
//                             core, coreLength);
//                    SubtractCore(bitsHigh, bitsHighLength,
//                                 bitsLow, bitsLowLength,
//                                 core, coreLength);

//                    // ... and finally merge the result! :-)
//                    AddSelf(bits + n, bitsLength - n, core, coreLength);
//                }
//                else
//                {
//                    fixed (uint* leftFold = new uint[leftFoldLength],
//                                 rightFold = new uint[rightFoldLength],
//                                 core = new uint[coreLength])
//                    {
//                        // ... compute z_a = a_1 + a_0 (call it fold...)
//                        Add(leftHigh, leftHighLength,
//                            leftLow, leftLowLength,
//                            leftFold, leftFoldLength);

//                        // ... compute z_b = b_1 + b_0 (call it fold...)
//                        Add(rightHigh, rightHighLength,
//                            rightLow, rightLowLength,
//                            rightFold, rightFoldLength);

//                        // ... compute z_1 = z_a * z_b - z_0 - z_2
//                        Multiply(leftFold, leftFoldLength,
//                                 rightFold, rightFoldLength,
//                                 core, coreLength);
//                        SubtractCore(bitsHigh, bitsHighLength,
//                                     bitsLow, bitsLowLength,
//                                     core, coreLength);

//                        // ... and finally merge the result! :-)
//                        AddSelf(bits + n, bitsLength - n, core, coreLength);
//                    }
//                }
//            }
//        }

//        private static unsafe void SubtractCore(uint* left, int leftLength,
//                                                uint* right, int rightLength,
//                                                uint* core, int coreLength)
//        {
//            Debug.Assert(leftLength >= 0);
//            Debug.Assert(rightLength >= 0);
//            Debug.Assert(coreLength >= 0);
//            Debug.Assert(leftLength >= rightLength);
//            Debug.Assert(coreLength >= leftLength);

//            // Executes a special subtraction algorithm for the multiplication,
//            // which needs to subtract two different values from a core value,
//            // while core is always bigger than the sum of these values.

//            // NOTE: we could do an ordinary subtraction of course, but we spare
//            // one "run", if we do this computation within a single one...

//            int i = 0;
//            long carry = 0L;

//            for (; i < rightLength; i++)
//            {
//                long digit = (core[i] + carry) - left[i] - right[i];
//                core[i] = unchecked((uint)digit);
//                carry = digit >> 32;
//            }
//            for (; i < leftLength; i++)
//            {
//                long digit = (core[i] + carry) - left[i];
//                core[i] = unchecked((uint)digit);
//                carry = digit >> 32;
//            }
//            for (; carry != 0 && i < coreLength; i++)
//            {
//                long digit = core[i] + carry;
//                core[i] = (uint)digit;
//                carry = digit >> 32;
//            }
//        }
//    }

//    public static uint[] Add(uint[] left, uint right)
//    {
//        Debug.Assert(left != null);
//        Debug.Assert(left.Length >= 1);

//        // Executes the addition for one big and one 32-bit integer.
//        // Thus, we've similar code than below, but there is no loop for
//        // processing the 32-bit integer, since it's a single element.

//        uint[] bits = new uint[left.Length + 1];

//        long digit = (long)left[0] + right;
//        bits[0] = unchecked((uint)digit);
//        long carry = digit >> 32;

//        for (int i = 1; i < left.Length; i++)
//        {
//            digit = left[i] + carry;
//            bits[i] = unchecked((uint)digit);
//            carry = digit >> 32;
//        }
//        bits[left.Length] = (uint)carry;

//        return bits;
//    }


//    public static uint[] Subtract(uint[] left, uint right)
//    {
//        Debug.Assert(left != null);
//        Debug.Assert(left.Length >= 1);
//        Debug.Assert(left[0] >= right || left.Length >= 2);

//        // Executes the subtraction for one big and one 32-bit integer.
//        // Thus, we've similar code than below, but there is no loop for
//        // processing the 32-bit integer, since it's a single element.

//        uint[] bits = new uint[left.Length];

//        long digit = (long)left[0] - right;
//        bits[0] = unchecked((uint)digit);
//        long carry = digit >> 32;

//        for (int i = 1; i < left.Length; i++)
//        {
//            digit = left[i] + carry;
//            bits[i] = unchecked((uint)digit);
//            carry = digit >> 32;
//        }

//        return bits;
//    }

//    public static int Compare(uint[] left, uint[] right)
//    {
//        Debug.Assert(left != null);
//        Debug.Assert(right != null);

//        if (left.Length < right.Length)
//            return -1;
//        if (left.Length > right.Length)
//            return 1;

//        for (int i = left.Length - 1; i >= 0; i--)
//        {
//            if (left[i] < right[i])
//                return -1;
//            if (left[i] > right[i])
//                return 1;
//        }

//        return 0;
//    }

//    public static uint[] Divide(uint[] left, uint right,
//                                    out uint remainder)
//    {
//        Debug.Assert(left != null);
//        Debug.Assert(left.Length >= 1);

//        // Executes the division for one big and one 32-bit integer.
//        // Thus, we've similar code than below, but there is no loop for
//        // processing the 32-bit integer, since it's a single element.

//        uint[] quotient = new uint[left.Length];

//        ulong carry = 0UL;
//        for (int i = left.Length - 1; i >= 0; i--)
//        {
//            ulong value = (carry << 32) | left[i];
//            ulong digit = value / right;
//            quotient[i] = (uint)digit;
//            carry = value - digit * right;
//        }
//        remainder = (uint)carry;

//        return quotient;
//    }

//    public static uint[] Divide(uint[] left, uint right)
//    {
//        Debug.Assert(left != null);
//        Debug.Assert(left.Length >= 1);

//        // Same as above, but only computing the quotient.

//        uint[] quotient = new uint[left.Length];

//        ulong carry = 0UL;
//        for (int i = left.Length - 1; i >= 0; i--)
//        {
//            ulong value = (carry << 32) | left[i];
//            ulong digit = value / right;
//            quotient[i] = (uint)digit;
//            carry = value - digit * right;
//        }

//        return quotient;
//    }

//    public static uint Remainder(uint[] left, uint right)
//    {
//        Debug.Assert(left != null);
//        Debug.Assert(left.Length >= 1);

//        // Same as above, but only computing the remainder.

//        ulong carry = 0UL;
//        for (int i = left.Length - 1; i >= 0; i--)
//        {
//            ulong value = (carry << 32) | left[i];
//            carry = value % right;
//        }

//        return (uint)carry;
//    }
//}

//}
//#endif

