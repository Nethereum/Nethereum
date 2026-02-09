using Nethereum.AccountAbstraction.ERC7579;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.ERC7579
{
    public class ERC7579ModeLibTests
    {
        [Fact]
        public void EncodeSingleDefault_ShouldReturnCorrectMode()
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();

            Assert.Equal(32, mode.Length);
            Assert.Equal((byte)CallType.Single, mode[0]);
            Assert.Equal((byte)ExecType.Default, mode[1]);
        }

        [Fact]
        public void EncodeBatchDefault_ShouldReturnCorrectMode()
        {
            var mode = ERC7579ModeLib.EncodeBatchDefault();

            Assert.Equal(32, mode.Length);
            Assert.Equal((byte)CallType.Batch, mode[0]);
            Assert.Equal((byte)ExecType.Default, mode[1]);
        }

        [Fact]
        public void EncodeDelegateCallDefault_ShouldReturnCorrectMode()
        {
            var mode = ERC7579ModeLib.EncodeDelegateCallDefault();

            Assert.Equal(32, mode.Length);
            Assert.Equal((byte)CallType.DelegateCall, mode[0]);
            Assert.Equal((byte)ExecType.Default, mode[1]);
        }

        [Fact]
        public void EncodeSingleTry_ShouldReturnCorrectMode()
        {
            var mode = ERC7579ModeLib.EncodeSingleTry();

            Assert.Equal(32, mode.Length);
            Assert.Equal((byte)CallType.Single, mode[0]);
            Assert.Equal((byte)ExecType.Try, mode[1]);
        }

        [Fact]
        public void EncodeBatchTry_ShouldReturnCorrectMode()
        {
            var mode = ERC7579ModeLib.EncodeBatchTry();

            Assert.Equal(32, mode.Length);
            Assert.Equal((byte)CallType.Batch, mode[0]);
            Assert.Equal((byte)ExecType.Try, mode[1]);
        }

        [Fact]
        public void EncodeMode_WithSelector_ShouldSetCorrectBytes()
        {
            var selector = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            var mode = ERC7579ModeLib.EncodeMode(CallType.Single, ExecType.Default, selector);

            Assert.Equal(32, mode.Length);
            Assert.Equal(0x12, mode[6]);
            Assert.Equal(0x34, mode[7]);
            Assert.Equal(0x56, mode[8]);
            Assert.Equal(0x78, mode[9]);
        }

        [Fact]
        public void EncodeMode_WithContext_ShouldSetCorrectBytes()
        {
            var context = new byte[] { 0xAA, 0xBB, 0xCC };
            var mode = ERC7579ModeLib.EncodeMode(CallType.Single, ExecType.Default, null, context);

            Assert.Equal(32, mode.Length);
            Assert.Equal(0xAA, mode[10]);
            Assert.Equal(0xBB, mode[11]);
            Assert.Equal(0xCC, mode[12]);
        }

        [Fact]
        public void DecodeMode_ShouldRoundTrip()
        {
            var selector = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            var context = new byte[22];
            context[0] = 0xAA;
            context[1] = 0xBB;

            var encoded = ERC7579ModeLib.EncodeMode(CallType.Batch, ExecType.Try, selector, context);
            var (callType, execType, decodedSelector, decodedContext) = ERC7579ModeLib.DecodeMode(encoded);

            Assert.Equal(CallType.Batch, callType);
            Assert.Equal(ExecType.Try, execType);
            Assert.Equal(selector, decodedSelector);
            Assert.Equal(0xAA, decodedContext[0]);
            Assert.Equal(0xBB, decodedContext[1]);
        }

        [Fact]
        public void IsSingleCall_ShouldReturnTrueForSingleMode()
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();
            Assert.True(ERC7579ModeLib.IsSingleCall(mode));
            Assert.False(ERC7579ModeLib.IsBatchCall(mode));
            Assert.False(ERC7579ModeLib.IsDelegateCall(mode));
        }

        [Fact]
        public void IsBatchCall_ShouldReturnTrueForBatchMode()
        {
            var mode = ERC7579ModeLib.EncodeBatchDefault();
            Assert.False(ERC7579ModeLib.IsSingleCall(mode));
            Assert.True(ERC7579ModeLib.IsBatchCall(mode));
            Assert.False(ERC7579ModeLib.IsDelegateCall(mode));
        }

        [Fact]
        public void IsDelegateCall_ShouldReturnTrueForDelegateMode()
        {
            var mode = ERC7579ModeLib.EncodeDelegateCallDefault();
            Assert.False(ERC7579ModeLib.IsSingleCall(mode));
            Assert.False(ERC7579ModeLib.IsBatchCall(mode));
            Assert.True(ERC7579ModeLib.IsDelegateCall(mode));
        }

        [Fact]
        public void IsTryExec_ShouldReturnTrueForTryMode()
        {
            var tryMode = ERC7579ModeLib.EncodeSingleTry();
            var defaultMode = ERC7579ModeLib.EncodeSingleDefault();

            Assert.True(ERC7579ModeLib.IsTryExec(tryMode));
            Assert.False(ERC7579ModeLib.IsTryExec(defaultMode));
        }
    }
}
