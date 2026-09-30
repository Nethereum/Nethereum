using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using static Nethereum.CoreChain.UnitTests.SystemCallBlockHarness;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// EIP-6110 §Block validity: a deposit log becomes a type <c>0x00</c> request in the EIP-7685
    /// list, so it changes the block's <c>requests_hash</c>. Removing the deposit half is
    /// invisible to every other test, because with no deposits that entry carries only its type
    /// byte and EIP-7685 excludes it - this is the test that sees it.
    /// </summary>
    public class DepositRequestsReachTheCommitmentTests
    {
        private const string PrivateKey =
            "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private static readonly BigInteger ChainId = 1;

        private static byte[] Word(int value)
        {
            var word = new byte[32];
            word[28] = (byte)(value >> 24);
            word[29] = (byte)(value >> 16);
            word[30] = (byte)(value >> 8);
            word[31] = (byte)value;
            return word;
        }

        private static byte[] DepositEventData(byte pubkeyByte)
        {
            var data = new List<byte>();
            data.AddRange(Word(160)); data.AddRange(Word(256)); data.AddRange(Word(320));
            data.AddRange(Word(384)); data.AddRange(Word(512));
            data.AddRange(Word(48)); data.AddRange(Enumerable.Repeat(pubkeyByte, 48)); data.AddRange(new byte[16]);
            data.AddRange(Word(32)); data.AddRange(Enumerable.Repeat((byte)0xb2, 32));
            data.AddRange(Word(8)); data.AddRange(Enumerable.Repeat((byte)0xd4, 8)); data.AddRange(new byte[24]);
            data.AddRange(Word(96)); data.AddRange(Enumerable.Repeat((byte)0xc3, 96));
            data.AddRange(Word(8)); data.AddRange(Enumerable.Repeat((byte)0xe5, 8)); data.AddRange(new byte[24]);
            return data.ToArray();
        }

        private static byte[] EmitsADepositLog(byte pubkeyByte)
        {
            var payload = DepositEventData(pubkeyByte);
            var prologue = new List<byte>();
            prologue.AddRange(new byte[] { 0x61, 0x02, 0x40 });
            prologue.AddRange(new byte[] { 0x61, 0x00, 0x31 });
            prologue.AddRange(new byte[] { 0x60, 0x00 });
            prologue.Add(0x39);
            prologue.Add(0x7f);
            prologue.AddRange(DepositRequests.DepositEventSignatureHash);
            prologue.AddRange(new byte[] { 0x61, 0x02, 0x40 });
            prologue.AddRange(new byte[] { 0x60, 0x00 });
            prologue.Add(0xa1);
            prologue.Add(0x00);

            Assert.Equal(0x31, prologue.Count);
            return prologue.Concat(payload).ToArray();
        }

        private static TxEntry CallTheDepositContract(BigInteger nonce) =>
            CallEmitter(DepositRequests.DepositContractAddress, nonce);

        private static TxEntry CallEmitter(string address, BigInteger nonce) =>
            new TxEntry(TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId,
                    address, 0, nonce, 1, 200_000, "")), SenderAddress);

        private static Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)> ExecuteWithDepositsAsync(
            int depositCount)
        {
            var txs = Enumerable.Range(0, depositCount).Select(i => CallTheDepositContract(i)).ToList();

            return ExecuteAsync(HardforkName.Amsterdam,
                Header(blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]),
                async store =>
                {
                    await DeployAsync(store, DepositRequests.DepositContractAddress, EmitsADepositLog(0xa1));
                    await store.SaveAccountAsync(SenderAddress,
                        new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
                },
                txs);
        }

        [Fact]
        [Trait("Category", "EIP6110")]
        public async Task Given_ABlockWhoseTransactionEmitsADepositLog_When_Executed_Then_ItCommitsToADepositRequest()
        {
            var (withDeposit, _) = await ExecuteWithDepositsAsync(depositCount: 1);
            var (withNone, _) = await ExecuteWithDepositsAsync(depositCount: 0);

            Assert.Null(withDeposit.Exception);
            Assert.NotEqual(withNone.ComputedRequestsHash.ToHex(), withDeposit.ComputedRequestsHash.ToHex());

            var expected = ExecutionRequests.ComputeRequestsHash(new[]
            {
                ExecutionRequests.Compose(ExecutionRequests.DepositRequestType,
                    DepositRequests.ExtractDepositData(DepositEventData(0xa1)))
            });
            Assert.Equal(expected.ToHex(), withDeposit.ComputedRequestsHash.ToHex());
        }

        /// <summary>
        /// EIP-6110 §Block validity: <i>"Beginning with the <c>FORK_BLOCK</c>, each deposit
        /// accumulated in the block MUST appear in the EIP-7685 requests list in the order they
        /// appear in the logs."</i> The two deposits carry different pubkeys, so a list built in
        /// the other order commits differently - with identical deposits the assertion would hold
        /// either way and prove nothing about order.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP6110")]
        public async Task Given_TwoDepositLogsInOneBlock_When_Executed_Then_BothAreCommittedInLogOrder()
        {
            var (two, _) = await ExecuteWithTwoDistinguishableDepositsAsync();

            var first = DepositRequests.ExtractDepositData(DepositEventData(0xa1));
            var second = DepositRequests.ExtractDepositData(DepositEventData(0xa2));

            Assert.Equal(InOrder(first, second).ToHex(), two.ComputedRequestsHash.ToHex());
            Assert.NotEqual(InOrder(second, first).ToHex(), two.ComputedRequestsHash.ToHex());
        }

        private static byte[] InOrder(byte[] first, byte[] second) =>
            ExecutionRequests.ComputeRequestsHash(new[]
            {
                ExecutionRequests.Compose(ExecutionRequests.DepositRequestType,
                    first.Concat(second).ToArray())
            });

        private static Task<(BlockExecutionResult Result, InMemoryStateStore StateStore)>
            ExecuteWithTwoDistinguishableDepositsAsync() =>
            ExecuteAsync(HardforkName.Amsterdam,
                Header(blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]),
                async store =>
                {
                    await DeployAsync(store, DepositRequests.DepositContractAddress,
                        EmitsTwoDepositLogs(0xa1, 0xa2));
                    await store.SaveAccountAsync(SenderAddress,
                        new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
                },
                new List<TxEntry> { CallTheDepositContract(0) });

        /// <summary>
        /// EIP-6110 counts a log only when its address is the deposit contract, so two
        /// distinguishable deposits must both be emitted by that contract - a second emitter at
        /// another address is correctly ignored and would prove nothing.
        /// </summary>
        private static byte[] EmitsTwoDepositLogs(byte firstPubkey, byte secondPubkey)
        {
            var first = DepositEventData(firstPubkey);
            var second = DepositEventData(secondPubkey);
            var prologue = new List<byte>();

            foreach (var payloadStart in new[] { 0, first.Length })
            {
                prologue.AddRange(new byte[] { 0x61, 0x02, 0x40 });
                prologue.AddRange(PushTwo(PayloadOffset + payloadStart));
                prologue.AddRange(new byte[] { 0x60, 0x00 });
                prologue.Add(0x39);
                prologue.Add(0x7f);
                prologue.AddRange(DepositRequests.DepositEventSignatureHash);
                prologue.AddRange(new byte[] { 0x61, 0x02, 0x40 });
                prologue.AddRange(new byte[] { 0x60, 0x00 });
                prologue.Add(0xa1);
            }

            prologue.Add(0x00);
            Assert.Equal(PayloadOffset, prologue.Count);

            return prologue.Concat(first).Concat(second).ToArray();
        }

        private const int PayloadOffset = 0x61;

        private static byte[] PushTwo(int value) =>
            new byte[] { 0x61, (byte)(value >> 8), (byte)value };
    }
}
