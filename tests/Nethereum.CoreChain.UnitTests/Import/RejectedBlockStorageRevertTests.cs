using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class RejectedBlockStorageRevertTests
    {
        private const string StoresTwelveThenReturnsAClearingContract =
            "6012600055" + "60066011600039" + "60066000f3" + "600060005500";

        private static ISignedTransaction Signed(BigInteger nonce, string to, string data) =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                BlockPipelineHarness.PrivateKey.HexToByteArray(),
                new Transaction1559(BlockPipelineHarness.ChainId, nonce, 2_000_000_000, 2_000_000_000, 200_000, to, 0, data, null)));

        [Fact]
        public async Task Given_ABlockThatClearsAStorageSlotAndIsRefusedAfterExecution_When_Imported_Then_TheSlotKeepsItsValue()
        {
            var contract = ContractUtils.CalculateContractAddress(BlockPipelineHarness.SenderAddress, 0);
            using var producer = await SideChainHarness.CreateAsync(withSideChain: false);
            using var node = await SideChainHarness.CreateAsync();
            var deploy = await producer.ProduceAsync(
                SideChainHarness.RecipientA,
                transactions: new List<ISignedTransaction> { Signed(0, "", StoresTwelveThenReturnsAClearingContract) });
            var clear = await producer.ProduceAsync(
                SideChainHarness.RecipientA,
                transactions: new List<ISignedTransaction> { Signed(1, contract, "") });
            Assert.True((await node.ImportAsync(deploy)).RootMatches);
            Assert.Equal(new BigInteger(0x12), await node.StorageAtAsync(contract, 0));
            var forged = BlockPipelineHarness.CloneHeader(clear.Header);
            forged.StateRoot = new byte[32];
            forged.StateRoot[0] = 9;

            var result = await node.Importer.ImportAsync(
                forged, clear.Transactions, uncles: null, clear.Withdrawals, CancellationToken.None);

            Assert.False(result.RootMatches);
            Assert.Equal(new BigInteger(0x12), await node.StorageAtAsync(contract, 0));
        }
    }
}
