using Nethereum.AccountAbstraction;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests
{
    public class AATransactionReceiptDiagnosticTests
    {
        private static UserOperationReceipt BuildReceipt(bool success, string reason) =>
            new UserOperationReceipt
            {
                UserOpHash = "0x" + "ab".PadLeft(64, '0'),
                Sender = "0x1111111111111111111111111111111111111111",
                Success = success,
                Reason = reason,
                ActualGasCost = new HexBigInteger(21000),
                ActualGasUsed = new HexBigInteger(21000)
            };

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "Read a successful UserOperation receipt", Order = 1)]
        public void SuccessfulOp_IsDiagnosedAsSucceeded()
        {
            var receipt = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(true, null));

            Assert.True(receipt.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.Succeeded, receipt.FailureKind);
            Assert.False(receipt.IsLikelyOutOfGas);
            Assert.Null(receipt.FailureDiagnostic);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "Diagnose a UserOperation that failed with no revert reason", Order = 2)]
        public void FailedWithNoReason_IsDiagnosedAsLikelyOutOfGas()
        {
            var receipt = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(false, null));

            Assert.False(receipt.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.FailedWithoutReason, receipt.FailureKind);
            Assert.True(receipt.IsLikelyOutOfGas);
            Assert.Contains("callGasLimit", receipt.FailureDiagnostic);
        }

        [Fact]
        public void FailedWithEmptyReason_IsDiagnosedAsLikelyOutOfGas()
        {
            var receipt = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(false, "   "));

            Assert.Equal(UserOperationFailureKind.FailedWithoutReason, receipt.FailureKind);
            Assert.True(receipt.IsLikelyOutOfGas);
        }

        [Fact]
        public void FailedWithBarePlaceholder_IsTreatedAsNoReason()
        {
            var receipt = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(false, "execution reverted"));

            Assert.Equal(UserOperationFailureKind.FailedWithoutReason, receipt.FailureKind);
            Assert.True(receipt.IsLikelyOutOfGas);
        }

        [Fact]
        public void FailedWithRevertReason_IsDiagnosedAsRevertedWithReason_NotOutOfGas()
        {
            var receipt = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(false, "count failed"));

            Assert.False(receipt.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.RevertedWithReason, receipt.FailureKind);
            Assert.False(receipt.IsLikelyOutOfGas);
            Assert.Contains("count failed", receipt.FailureDiagnostic);
        }
    }
}
