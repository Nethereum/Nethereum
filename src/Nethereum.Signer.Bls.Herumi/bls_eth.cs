/**
	@file
	@brief C# interface of BLS signature
	@author MITSUNARI Shigeo(@herumi)
	@license modified new BSD license
	http://opensource.org/licenses/BSD-3-Clause
    @note
    use bls384_256 built by `mklib dll eth` to use Ethereum mode
*/
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace mcl
{
    public class BLS
    {
        public const int BN254 = 0;
        public const int BLS12_381 = 5;
        public const bool isETH = true;

        const int IoEcComp = 512; // fixed byte representation
        public const int FR_UNIT_SIZE = 4;
        public const int FP_UNIT_SIZE = 6;
        public const int BLS_COMPILER_TIME_VAR_ADJ = isETH ? 200 : 0;
        public const int COMPILED_TIME_VAR = FR_UNIT_SIZE * 10 + FP_UNIT_SIZE + BLS_COMPILER_TIME_VAR_ADJ;

        public const int ID_UNIT_SIZE = FR_UNIT_SIZE;
        public const int SECRETKEY_UNIT_SIZE = FR_UNIT_SIZE;
        public const int PUBLICKEY_UNIT_SIZE = FP_UNIT_SIZE * 3 * (isETH ? 1 : 2);
        public const int SIGNATURE_UNIT_SIZE = FP_UNIT_SIZE * 3 * (isETH ? 2 : 1);

        public const int ID_SERIALIZE_SIZE = ID_UNIT_SIZE * 8;
        public const int SECRETKEY_SERIALIZE_SIZE = SECRETKEY_UNIT_SIZE * 8;
        public const int PUBLICKEY_SERIALIZE_SIZE = PUBLICKEY_UNIT_SIZE * 8;
        public const int SIGNATURE_SERIALIZE_SIZE = SIGNATURE_UNIT_SIZE * 8;
        public const int MSG_SIZE = 32;

        public const string dllName = "bls_eth";
        [DllImport(dllName)] public static extern int blsInit(int curveType, int compiledTimeVar);
        [DllImport(dllName)] public static extern int blsGetFrByteSize();
        [DllImport(dllName)] public static extern int blsGetG1ByteSize();

        [DllImport(dllName)] public static extern void blsIdSetInt(ref Id id, int x);
        [DllImport(dllName)] public static extern int blsIdSetDecStr(ref Id id, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        [DllImport(dllName)] public static extern int blsIdSetHexStr(ref Id id, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        [DllImport(dllName)] public static extern ulong blsIdGetDecStr([Out]StringBuilder buf, ulong maxBufSize, in Id id);
        [DllImport(dllName)] public static extern ulong blsIdGetHexStr([Out]StringBuilder buf, ulong maxBufSize, in Id id);

        [DllImport(dllName)] public static extern ulong blsIdSerialize([Out]byte[] buf, ulong maxBufSize, in Id id);
        [DllImport(dllName)] public static extern ulong blsSecretKeySerialize([Out]byte[] buf, ulong maxBufSize, in SecretKey sec);
        [DllImport(dllName)] public static extern ulong blsPublicKeySerialize([Out]byte[] buf, ulong maxBufSize, in PublicKey pub);
        [DllImport(dllName)] public static extern ulong blsSignatureSerialize([Out]byte[] buf, ulong maxBufSize, in Signature sig);
        [DllImport(dllName)] public static extern ulong blsIdDeserialize(ref Id id, [In]byte[] buf, ulong bufSize);
        [DllImport(dllName)] public static extern ulong blsSecretKeyDeserialize(ref SecretKey sec, [In]byte[] buf, ulong bufSize);
        [DllImport(dllName)] public static extern ulong blsPublicKeyDeserialize(ref PublicKey pub, [In]byte[] buf, ulong bufSize);
        [DllImport(dllName)] public static extern ulong blsSignatureDeserialize(ref Signature sig, [In]byte[] buf, ulong bufSize);

        [DllImport(dllName)] public static extern int blsIdIsEqual(in Id lhs, in Id rhs);
        [DllImport(dllName)] public static extern int blsSecretKeyIsEqual(in SecretKey lhs, in SecretKey rhs);
        [DllImport(dllName)] public static extern int blsPublicKeyIsEqual(in PublicKey lhs, in PublicKey rhs);
        [DllImport(dllName)] public static extern int blsSignatureIsEqual(in Signature lhs, in Signature rhs);
        // add
        [DllImport(dllName)] public static extern void blsSecretKeyAdd(ref SecretKey sec, in SecretKey rhs);
        [DllImport(dllName)] public static extern void blsPublicKeyAdd(ref PublicKey pub, in PublicKey rhs);
        [DllImport(dllName)] public static extern void blsSignatureAdd(ref Signature sig, in Signature rhs);
        // sub
        [DllImport(dllName)] public static extern void blsSecretKeySub(ref SecretKey sec, in SecretKey rhs);
        [DllImport(dllName)] public static extern void blsPublicKeySub(ref PublicKey pub, in PublicKey rhs);
        [DllImport(dllName)] public static extern void blsSignatureSub(ref Signature sig, in Signature rhs);

        // neg
        [DllImport(dllName)] public static extern void blsSecretKeyNeg(ref SecretKey x);
        [DllImport(dllName)] public static extern void blsPublicKeyNeg(ref PublicKey x);
        [DllImport(dllName)] public static extern void blsSignatureNeg(ref Signature x);
        // mul Fr
        [DllImport(dllName)] public static extern void blsSecretKeyMul(ref SecretKey sec, in SecretKey rhs);
        [DllImport(dllName)] public static extern void blsPublicKeyMul(ref PublicKey pub, in SecretKey rhs);
        [DllImport(dllName)] public static extern void blsSignatureMul(ref Signature sig, in SecretKey rhs);

        // mulVec
        [DllImport(dllName)] public static extern int blsPublicKeyMulVec(ref PublicKey pub, in PublicKey pubVec, in SecretKey idVec, ulong n);
        [DllImport(dllName)] public static extern int blsSignatureMulVec(ref Signature sig, in Signature sigVec, in SecretKey idVec, ulong n);
        // zero
        [DllImport(dllName)] public static extern int blsSecretKeyIsZero(in SecretKey x);
        [DllImport(dllName)] public static extern int blsPublicKeyIsZero(in PublicKey x);
        [DllImport(dllName)] public static extern int blsSignatureIsZero(in Signature x);
        //	hash buf and set
        [DllImport(dllName)] public static extern int blsHashToSecretKey(ref SecretKey sec, [In]byte[] buf, ulong bufSize);
        /*
			set secretKey if system has /dev/urandom or CryptGenRandom
			return 0 if success else -1
		*/
        [DllImport(dllName)] public static extern int blsSecretKeySetByCSPRNG(ref SecretKey sec);

        [DllImport(dllName)] public static extern void blsGetPublicKey(ref PublicKey pub, in SecretKey sec);
        [DllImport(dllName)] public static extern void blsGetPop(ref Signature sig, in SecretKey sec);

        // return 0 if success
        [DllImport(dllName)] public static extern int blsSecretKeyShare(ref SecretKey sec, in SecretKey msk, ulong k, in Id id);
        [DllImport(dllName)] public static extern int blsPublicKeyShare(ref PublicKey pub, in PublicKey mpk, ulong k, in Id id);


        [DllImport(dllName)] public static extern int blsSecretKeyRecover(ref SecretKey sec, in SecretKey secVec, in Id idVec, ulong n);
        [DllImport(dllName)] public static extern int blsPublicKeyRecover(ref PublicKey pub, in PublicKey pubVec, in Id idVec, ulong n);
        [DllImport(dllName)] public static extern int blsSignatureRecover(ref Signature sig, in Signature sigVec, in Id idVec, ulong n);

        [DllImport(dllName)] public static extern void blsSign(ref Signature sig, in SecretKey sec, [In]byte[] buf, ulong size);

        // return 1 if valid
        [DllImport(dllName)] public static extern int blsVerify(in Signature sig, in PublicKey pub, [In]byte[] buf, ulong size);
        [DllImport(dllName)] public static extern int blsVerifyPop(in Signature sig, in PublicKey pub);

        //////////////////////////////////////////////////////////////////////////
        // the following apis will be removed

        // mask buf with (1 << (bitLen(r) - 1)) - 1 if buf >= r
        [DllImport(dllName)] public static extern int blsIdSetLittleEndian(ref Id id, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        /*
			return written byte size if success else 0
		*/
        [DllImport(dllName)] public static extern ulong blsIdGetLittleEndian([Out]StringBuilder buf, ulong maxBufSize, in Id id);

        // return 0 if success
        // mask buf with (1 << (bitLen(r) - 1)) - 1 if buf >= r
        [DllImport(dllName)] public static extern int blsSecretKeySetLittleEndian(ref SecretKey sec, [In]byte[] buf, ulong bufSize);
        [DllImport(dllName)] public static extern int blsSecretKeySetDecStr(ref SecretKey sec, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        [DllImport(dllName)] public static extern int blsSecretKeySetHexStr(ref SecretKey sec, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        /*
			return written byte size if success else 0
		*/
        [DllImport(dllName)] public static extern ulong blsSecretKeyGetLittleEndian([Out]byte[] buf, ulong maxBufSize, in SecretKey sec);
        /*
			return strlen(buf) if success else 0
			buf is '\0' terminated
		*/
        [DllImport(dllName)] public static extern ulong blsSecretKeyGetDecStr([Out]StringBuilder buf, ulong maxBufSize, in SecretKey sec);
        [DllImport(dllName)] public static extern ulong blsSecretKeyGetHexStr([Out]StringBuilder buf, ulong maxBufSize, in SecretKey sec);
        [DllImport(dllName)] public static extern int blsPublicKeySetHexStr(ref PublicKey pub, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        [DllImport(dllName)] public static extern ulong blsPublicKeyGetHexStr([Out]StringBuilder buf, ulong maxBufSize, in PublicKey pub);
        [DllImport(dllName)] public static extern int blsSignatureSetHexStr(ref Signature sig, [In][MarshalAs(UnmanagedType.LPStr)] string buf, ulong bufSize);
        [DllImport(dllName)] public static extern ulong blsSignatureGetHexStr([Out]StringBuilder buf, ulong maxBufSize, in Signature sig);

        [DllImport(dllName)] public static extern int blsFastAggregateVerify(in Signature sig, in PublicKey pubVec, ulong n, [In]byte[] msg, ulong msgSize);
        [DllImport(dllName)] public static extern int blsAggregateVerifyNoCheck(in Signature sig, in PublicKey pubVec, in Msg msgVec, ulong msgSize, ulong n);

        // don't call this if isETH = true, it calls in BLS()
        public static void Init(int curveType = BLS12_381) {
            if (isETH && isInit) return;
            if (isETH && curveType != BLS12_381) {
                throw new PlatformNotSupportedException("bad curveType");
            }
            if (!System.Environment.Is64BitProcess) {
                throw new PlatformNotSupportedException("not 64-bit system");
            }
            int err = blsInit(curveType, COMPILED_TIME_VAR);
            if (err != 0) {
                throw new ArgumentException("blsInit");
            }
        }
        static readonly bool isInit;
        // call at once
        static BLS()
        {
            if (isETH) {
                Init(BLS12_381);
                isInit = true;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct Id
        {
            private fixed ulong v[ID_UNIT_SIZE];
            public byte[] Serialize() {
                ulong bufSize = (ulong)blsGetFrByteSize();
                byte[] buf = new byte[bufSize];
                ulong n = blsIdSerialize(buf, bufSize, this);
                if (n != bufSize) {
                    throw new ArithmeticException("blsIdSerialize");
                }
                return buf;
            }
            public void Deserialize(byte[] buf) {
                ulong bufSize = (ulong)buf.Length;
                ulong n = blsIdDeserialize(ref this, buf, bufSize);
                if (n == 0 || n != bufSize) {
                    throw new ArithmeticException("blsIdDeserialize");
                }
            }
            public bool IsEqual(in Id rhs) {
                return blsIdIsEqual(this, rhs) != 0;
            }
            public void SetDecStr(string s) {
                if (blsIdSetDecStr(ref this, s, (ulong)s.Length) != 0) {
                    throw new ArgumentException("blsIdSetDecStr:" + s);
                }
            }
            public void SetHexStr(string s) {
                if (blsIdSetHexStr(ref this, s, (ulong)s.Length) != 0) {
                    throw new ArgumentException("blsIdSetHexStr:" + s);
                }
            }
            public void SetInt(int x) {
                blsIdSetInt(ref this, x);
            }
            public string GetDecStr() {
                StringBuilder sb = new StringBuilder(1024);
                ulong size = blsIdGetDecStr(sb, (ulong)sb.Capacity, this);
                if (size == 0) {
                    throw new ArgumentException("blsIdGetDecStr");
                }
                return sb.ToString(0, (int)size);
            }
            public string GetHexStr() {
                StringBuilder sb = new StringBuilder(1024);
                ulong size = blsIdGetHexStr(sb, (ulong)sb.Capacity, this);
                if (size == 0) {
                    throw new ArgumentException("blsIdGetHexStr");
                }
                return sb.ToString(0, (int)size);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct SecretKey
        {
            private fixed ulong v[SECRETKEY_UNIT_SIZE];
            public byte[] Serialize() {
                ulong bufSize = (ulong)blsGetFrByteSize();
                byte[] buf = new byte[bufSize];
                ulong n = blsSecretKeySerialize(buf, bufSize, this);
                if (n != bufSize) {
                    throw new ArithmeticException("blsSecretKeySerialize");
                }
                return buf;
            }
            public void Deserialize(byte[] buf) {
                ulong bufSize = (ulong)buf.Length;
                ulong n = blsSecretKeyDeserialize(ref this, buf, bufSize);
                if (n == 0 || n != bufSize) {
                    throw new ArithmeticException("blsSecretKeyDeserialize");
                }
            }
            public bool IsEqual(in SecretKey rhs) {
                return blsSecretKeyIsEqual(this, rhs) != 0;
            }
            public bool IsZero()
            {
                return blsSecretKeyIsZero(this) != 0;
            }
            public void SetHexStr(string s) {
                if (blsSecretKeySetHexStr(ref this, s, (ulong)s.Length) != 0) {
                    throw new ArgumentException("blsSecretKeySetHexStr:" + s);
                }
            }
            public string GetHexStr() {
                StringBuilder sb = new StringBuilder(1024);
                ulong size = blsSecretKeyGetHexStr(sb, (ulong)sb.Capacity, this);
                if (size == 0) {
                    throw new ArgumentException("blsSecretKeyGetHexStr");
                }
                return sb.ToString(0, (int)size);
            }
            public void Add(in SecretKey rhs) {
                blsSecretKeyAdd(ref this, rhs);
            }
            public void Sub(in SecretKey rhs)
            {
                blsSecretKeySub(ref this, rhs);
            }
            public void Neg()
            {
                blsSecretKeyNeg(ref this);
            }
            public void Mul(in SecretKey rhs)
            {
                blsSecretKeyMul(ref this, rhs);
            }
            public void SetByCSPRNG() {
                blsSecretKeySetByCSPRNG(ref this);
            }
            public void SetHashOf(byte[] buf)
            {
                if (blsHashToSecretKey(ref this, buf, (ulong)buf.Length) != 0) {
                    throw new ArgumentException("blsHashToSecretKey");
                }
            }
            public void SetHashOf(string s) {
                SetHashOf(Encoding.UTF8.GetBytes(s));
            }
            public PublicKey GetPublicKey() {
                PublicKey pub;
                blsGetPublicKey(ref pub, this);
                return pub;
            }
            public Signature Sign(byte[] buf)
            {
                Signature sig;
                blsSign(ref sig, this, buf, (ulong)buf.Length);
                return sig;
            }
            public Signature Sign(string s)
            {
                return Sign(Encoding.UTF8.GetBytes(s));
            }
            public Signature GetPop() {
                Signature sig;
                blsGetPop(ref sig, this);
                return sig;
            }
        }
        // secretKey = sum_{i=0}^{msk.Length - 1} msk[i] * id^i
        public static SecretKey ShareSecretKey(in SecretKey[] msk, in Id id) {
            SecretKey sec;
            if (blsSecretKeyShare(ref sec, msk[0], (ulong)msk.Length, id) != 0) {
                throw new ArgumentException("GetSecretKeyForId:" + id.ToString());
            }
            return sec;
        }
        public static SecretKey RecoverSecretKey(in SecretKey[] secVec, in Id[] idVec) {
            SecretKey sec;
            if (blsSecretKeyRecover(ref sec, secVec[0], idVec[0], (ulong)secVec.Length) != 0) {
                throw new ArgumentException("Recover");
            }
            return sec;
        }
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct PublicKey
        {
            private fixed ulong v[PUBLICKEY_UNIT_SIZE];
            public byte[] Serialize() {
                ulong bufSize = (ulong)blsGetG1ByteSize() * (isETH ? 1 : 2);
                byte[] buf = new byte[bufSize];
                ulong n = blsPublicKeySerialize(buf, bufSize, this);
                if (n != bufSize) {
                    throw new ArithmeticException("blsPublicKeySerialize");
                }
                return buf;
            }
            public void Deserialize(byte[] buf) {
                ulong bufSize = (ulong)buf.Length;
                ulong n = blsPublicKeyDeserialize(ref this, buf, bufSize);
                if (n == 0 || n != bufSize) {
                    throw new ArithmeticException("blsPublicKeyDeserialize");
                }
            }
            public bool IsEqual(in PublicKey rhs) {
                return blsPublicKeyIsEqual(this, rhs) != 0;
            }
            public bool IsZero()
            {
                return blsPublicKeyIsZero(this) != 0;
            }
            public void SetStr(string s) {
                if (blsPublicKeySetHexStr(ref this, s, (ulong)s.Length) != 0) {
                    throw new ArgumentException("blsPublicKeySetStr:" + s);
                }
            }
            public string GetHexStr() {
                StringBuilder sb = new StringBuilder(1024);
                ulong size = blsPublicKeyGetHexStr(sb, (ulong)sb.Capacity, this);
                if (size == 0) {
                    throw new ArgumentException("blsPublicKeyGetStr");
                }
                return sb.ToString(0, (int)size);
            }
            public void Add(in PublicKey rhs) {
                blsPublicKeyAdd(ref this, rhs);
            }
            public void Sub(in PublicKey rhs)
            {
                blsPublicKeySub(ref this, rhs);
            }
            public void Neg()
            {
                blsPublicKeyNeg(ref this);
            }
            public void Mul(in SecretKey rhs)
            {
                blsPublicKeyMul(ref this, rhs);
            }
            public bool Verify(in Signature sig, byte[] buf)
            {
                return blsVerify(sig, this, buf, (ulong)buf.Length) == 1;
            }
            public bool Verify(in Signature sig, string s) {
                return Verify(sig, Encoding.UTF8.GetBytes(s));
            }
            public bool VerifyPop(in Signature pop) {
                return blsVerifyPop(pop, this) == 1;
            }
        }
        // publicKey = sum_{i=0}^{mpk.Length - 1} mpk[i] * id^i
        public static PublicKey SharePublicKey(in PublicKey[] mpk, in Id id) {
            PublicKey pub;
            if (blsPublicKeyShare(ref pub, mpk[0], (ulong)mpk.Length, id) != 0) {
                throw new ArgumentException("GetPublicKeyForId:" + id.ToString());
            }
            return pub;
        }
        public static PublicKey RecoverPublicKey(in PublicKey[] pubVec, in Id[] idVec) {
            PublicKey pub;
            if (blsPublicKeyRecover(ref pub, pubVec[0], idVec[0], (ulong)pubVec.Length) != 0) {
                throw new ArgumentException("Recover");
            }
            return pub;
        }
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct Signature
        {
            private fixed ulong v[SIGNATURE_UNIT_SIZE];
            public byte[] Serialize() {
                ulong bufSize = (ulong)blsGetG1ByteSize() * (isETH ? 2 : 1);
                byte[] buf = new byte[bufSize];
                ulong n = blsSignatureSerialize(buf, bufSize, this);
                if (n != bufSize) {
                    throw new ArithmeticException("blsSignatureSerialize");
                }
                return buf;
            }
            public void Deserialize(byte[] buf) {
                ulong bufSize = (ulong)buf.Length;
                ulong n = blsSignatureDeserialize(ref this, buf, bufSize);
                if (n == 0 || n != bufSize) {
                    throw new ArithmeticException("blsSignatureDeserialize");
                }
            }
            public bool IsEqual(in Signature rhs) {
                return blsSignatureIsEqual(this, rhs) != 0;
            }
            public bool IsZero()
            {
                return blsSignatureIsZero(this) != 0;
            }
            public void SetStr(string s) {
                if (blsSignatureSetHexStr(ref this, s, (ulong)s.Length) != 0) {
                    throw new ArgumentException("blsSignatureSetStr:" + s);
                }
            }
            public string GetHexStr() {
                StringBuilder sb = new StringBuilder(1024);
                ulong size = blsSignatureGetHexStr(sb, (ulong)sb.Capacity, this);
                if (size == 0) {
                    throw new ArgumentException("blsSignatureGetStr");
                }
                return sb.ToString(0, (int)size);
            }
            public void Add(in Signature rhs) {
                blsSignatureAdd(ref this, rhs);
            }
            public void Sub(in Signature rhs)
            {
                blsSignatureSub(ref this, rhs);
            }
            public void Neg()
            {
                blsSignatureNeg(ref this);
            }
            public void Mul(in SecretKey rhs)
            {
                blsSignatureMul(ref this, rhs);
            }
        }
        public static Signature RecoverSign(in Signature[] sigVec, in Id[] idVec) {
            Signature sig;
            if (blsSignatureRecover(ref sig, sigVec[0], idVec[0], (ulong)sigVec.Length) != 0) {
                throw new ArgumentException("Recover");
            }
            return sig;
        }
        public static PublicKey MulVec(in PublicKey[] pubVec, in SecretKey[] secVec)
        {
            if (pubVec.Length != secVec.Length) {
                throw new ArithmeticException("PublicKey.MulVec");
            }
            PublicKey pub;
            blsPublicKeyMulVec(ref pub, pubVec[0], secVec[0], (ulong)pubVec.Length);
            return pub;
        }
        public static Signature MulVec(in Signature[] sigVec, in SecretKey[] secVec)
        {
            if (sigVec.Length != secVec.Length) {
                throw new ArithmeticException("Signature.MulVec");
            }
            Signature sig;
            blsSignatureMulVec(ref sig, sigVec[0], secVec[0], (ulong)sigVec.Length);
            return sig;
        }
        public static bool FastAggregateVerify(in Signature sig, in PublicKey[] pubVec, byte[] msg)
        {
            if (pubVec.Length == 0) {
                throw new ArgumentException("pubVec is empty");
            }
            return blsFastAggregateVerify(in sig, in pubVec[0], (ulong)pubVec.Length, msg, (ulong)msg.Length) == 1;
        }
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct Msg
        {
            private fixed byte v[MSG_SIZE];
            public void Set(byte[] buf) {
                if (buf.Length != MSG_SIZE) {
                    throw new ArgumentException("bad buf size");
                }
                fixed (byte *p = v) {
                    for (int i = 0; i < MSG_SIZE; i++) {
                        p[i] = buf[i];
                    }
                }
            }
            public byte Get(int i)
            {
                fixed (byte *p = v) {
                    return p[i];
                }
            }
            public override int GetHashCode()
            {
                // FNV-1a 32-bit hash
                uint v = 2166136261;
                for (int i = 0; i < MSG_SIZE; i++) {
                    v ^= Get(i);
                    v *= 16777619;
                }
                return (int)v;
            }
            public override bool Equals(object obj)
            {
                if (!(obj is Msg)) return false;
                var rhs = (Msg)obj;
                for (int i = 0; i < MSG_SIZE; i++) {
                    if (Get(i) != rhs.Get(i)) return false;
                }
                return true;
            }
        }
        public static bool AreAllMsgDifferent(in Msg[] msgVec)
        {
            var set = new HashSet<Msg>();
            foreach (var msg in msgVec) {
                if (!set.Add(msg)) return false;
            }
            return true;
        }
        public static bool AggregateVerifyNoCheck(in Signature sig, in PublicKey[] pubVec, in Msg[] msgVec)
        {
            if (pubVec.Length != msgVec.Length) {
                    throw new ArgumentException("different length of pubVec and msgVec");
            }
            ulong n = (ulong)pubVec.Length;
            if (n == 0) {
                throw new ArgumentException("pubVec is empty");
            }
            return blsAggregateVerifyNoCheck(in sig, in pubVec[0], in msgVec[0], MSG_SIZE, n) == 1;
        }
        public static bool AggregateVerify(in Signature sig, in PublicKey[] pubVec, in Msg[] msgVec)
        {
            if (!AreAllMsgDifferent(msgVec)) {
                return false;
            }
            return AggregateVerifyNoCheck(in sig, in pubVec, in msgVec);
        }
    }
}
