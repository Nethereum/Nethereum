using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class ReceiptRevertReasonDecodeTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string Sender = "0x1111111111111111111111111111111111111111";
        private const string Paymaster = "0x0000000000000000000000000000000000000000";

        private static readonly string UserOpEventSig =
            (string)Event<UserOperationEventEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static readonly string RevertReasonSig =
            (string)Event<UserOperationRevertReasonEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static string FullUserOpHash =>
            "0x" + "ab".PadLeft(64, '0');

        [Fact]
        public void RevertedOpReceipt_CarriesDecodedReason_NotRawHex()
        {
            const string humanReason = "ERC20: transfer amount exceeds balance";
            var revertBytes = EncodeStandardErrorRevert(humanReason);

            var receipt = BuildReceipt(revertBytes);

            var result = new UserOperationReceiptService(new Web3.Web3("http://localhost:8545"))
                .BuildFromTransactionReceipt(receipt, FullUserOpHash, EntryPoint);

            Assert.NotNull(result);
            Assert.False(result!.Success);
            Assert.Equal(humanReason, result.Reason);
            Assert.NotEqual(revertBytes.ToHex(true), result.Reason);
        }

        [Fact]
        public void RevertedOpReceipt_NonStandardRevertData_FallsBackToHex()
        {
            var revertBytes = new byte[] { 0xde, 0xad, 0xbe, 0xef };

            var receipt = BuildReceipt(revertBytes);

            var result = new UserOperationReceiptService(new Web3.Web3("http://localhost:8545"))
                .BuildFromTransactionReceipt(receipt, FullUserOpHash, EntryPoint);

            Assert.NotNull(result);
            Assert.Equal(revertBytes.ToHex(true), result!.Reason);
        }

        private static TransactionReceipt BuildReceipt(byte[] revertReason)
        {
            var revertLog = new FilterLog
            {
                Address = EntryPoint,
                LogIndex = new HexBigInteger(0),
                TransactionHash = "0x" + new string('1', 64),
                Topics = new object[] { RevertReasonSig, FullUserOpHash, PadAddress(Sender) },
                Data = "0x" + new ABIEncode().GetABIEncoded(
                    new ABIValue("uint256", BigInteger.Zero),
                    new ABIValue("bytes", revertReason)).ToHex()
            };

            var userOpEventLog = new FilterLog
            {
                Address = EntryPoint,
                LogIndex = new HexBigInteger(1),
                TransactionHash = "0x" + new string('1', 64),
                Topics = new object[] { UserOpEventSig, FullUserOpHash, PadAddress(Sender), PadAddress(Paymaster) },
                Data = "0x" + new ABIEncode().GetABIEncoded(
                    new ABIValue("uint256", BigInteger.Zero),
                    new ABIValue("bool", false),
                    new ABIValue("uint256", new BigInteger(21000)),
                    new ABIValue("uint256", new BigInteger(21000))).ToHex()
            };

            return new TransactionReceipt
            {
                TransactionHash = "0x" + new string('1', 64),
                BlockNumber = new HexBigInteger(100),
                BlockHash = "0x" + new string('2', 64),
                Logs = new[] { revertLog, userOpEventLog }
            };
        }

        private static byte[] EncodeStandardErrorRevert(string message)
        {
            var hex = new FunctionCallEncoder().EncodeRequest(
                "0x08c379a0",
                new[] { new Parameter("string", "message", 1) },
                message);
            return hex.HexToByteArray();
        }

        private static string PadAddress(string address) =>
            "0x000000000000000000000000" + address.Substring(2).ToLowerInvariant();
    }
}
