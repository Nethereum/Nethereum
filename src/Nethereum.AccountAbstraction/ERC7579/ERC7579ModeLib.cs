using System;

namespace Nethereum.AccountAbstraction.ERC7579
{
    public enum CallType : byte
    {
        Single = 0x00,
        Batch = 0x01,
        DelegateCall = 0xFF
    }

    public enum ExecType : byte
    {
        Default = 0x00,
        Try = 0x01
    }

    public static class ERC7579ModeLib
    {
        public static byte[] EncodeMode(
            CallType callType,
            ExecType execType,
            byte[] modeSelector = null,
            byte[] context = null)
        {
            var mode = new byte[32];
            mode[0] = (byte)callType;
            mode[1] = (byte)execType;

            if (modeSelector != null && modeSelector.Length == 4)
            {
                Array.Copy(modeSelector, 0, mode, 6, 4);
            }

            if (context != null)
            {
                var copyLength = Math.Min(context.Length, 22);
                Array.Copy(context, 0, mode, 10, copyLength);
            }

            return mode;
        }

        public static byte[] EncodeSingleDefault()
        {
            return EncodeMode(CallType.Single, ExecType.Default);
        }

        public static byte[] EncodeBatchDefault()
        {
            return EncodeMode(CallType.Batch, ExecType.Default);
        }

        public static byte[] EncodeDelegateCallDefault()
        {
            return EncodeMode(CallType.DelegateCall, ExecType.Default);
        }

        public static byte[] EncodeSingleTry()
        {
            return EncodeMode(CallType.Single, ExecType.Try);
        }

        public static byte[] EncodeBatchTry()
        {
            return EncodeMode(CallType.Batch, ExecType.Try);
        }

        public static (CallType callType, ExecType execType, byte[] modeSelector, byte[] context) DecodeMode(byte[] mode)
        {
            if (mode == null || mode.Length != 32)
            {
                throw new ArgumentException("Mode must be 32 bytes", nameof(mode));
            }

            var callType = (CallType)mode[0];
            var execType = (ExecType)mode[1];

            var modeSelector = new byte[4];
            Array.Copy(mode, 6, modeSelector, 0, 4);

            var context = new byte[22];
            Array.Copy(mode, 10, context, 0, 22);

            return (callType, execType, modeSelector, context);
        }

        public static bool IsSingleCall(byte[] mode)
        {
            return mode != null && mode.Length == 32 && mode[0] == (byte)CallType.Single;
        }

        public static bool IsBatchCall(byte[] mode)
        {
            return mode != null && mode.Length == 32 && mode[0] == (byte)CallType.Batch;
        }

        public static bool IsDelegateCall(byte[] mode)
        {
            return mode != null && mode.Length == 32 && mode[0] == (byte)CallType.DelegateCall;
        }

        public static bool IsTryExec(byte[] mode)
        {
            return mode != null && mode.Length == 32 && mode[1] == (byte)ExecType.Try;
        }
    }
}
