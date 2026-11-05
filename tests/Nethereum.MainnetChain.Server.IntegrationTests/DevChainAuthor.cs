using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Genesis;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Node.HarnessServer;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Signer;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    internal sealed record AuthoredBlock(
        ExecutionPayloadV3 Payload, string[] ExecutionRequests, byte[] Hash, byte[] StateRoot, ulong Number);

    internal sealed class DevChainAuthor : IAsyncDisposable
    {
        public const string ZeroRoot = "0x0000000000000000000000000000000000000000000000000000000000000000";
        public const string Recipient = "0x000000000000000000000000000000000000dEaD";

        private readonly HarnessNode _devchain;
        private readonly BigInteger _chainId;
        private int _nonce;

        private DevChainAuthor(HarnessNode devchain, BigInteger chainId)
        {
            _devchain = devchain;
            _chainId = chainId;
        }

        public byte[] GenesisHash { get; private set; } = Array.Empty<byte>();

        public byte[] GenesisStateRoot { get; private set; } = Array.Empty<byte>();

        public static async Task<DevChainAuthor> CreateAsync(StandardGenesisDocument genesis)
        {
            var devchain = await HarnessNodeHost.ComposeInProcessAsync(genesis);
            var author = new DevChainAuthor(devchain, genesis.Config.ChainId ?? 1);
            author.GenesisHash = await devchain.Stores.Blocks.GetHashByNumberAsync(BigInteger.Zero);
            author.GenesisStateRoot = (await devchain.Stores.Blocks.GetByNumberAsync(0)).StateRoot;
            return author;
        }

        public async Task<AuthoredBlock> ProduceAsync(int transfers = 1, bool distinctRecipients = false)
        {
            var head = await _devchain.Stores.Blocks.GetLatestAsync();
            var headHash = await _devchain.Stores.Blocks.GetHashByNumberAsync((BigInteger)head.BlockNumber);
            var key = new EthECKey(CustomGenesisFixture.FundedPrivateKey);

            var transactions = new List<ISignedTransaction>();
            for (var i = 0; i < transfers; i++) transactions.Add(SignedTransfer(key, _nonce++, distinctRecipients ? DistinctRecipient(i) : Recipient));

            var attributes = new PayloadAttributesV3
            {
                Timestamp = new HexBigInteger(head.Timestamp + 12),
                PrevRandao = "0x" + new string('7', 64),
                SuggestedFeeRecipient = "0x0000000000000000000000000000000000009999",
                ParentBeaconBlockRoot = ZeroRoot,
                Withdrawals = new List<Nethereum.RPC.Eth.DTOs.Withdrawal>()
            };

            var forkchoice = await _devchain.Engine.ForkchoiceUpdatedAsync(StateOf(headHash), attributes, transactions);
            var built = await _devchain.Engine.GetPayloadV4Async(forkchoice.PayloadId);
            var requests = built.ExecutionRequests?.ToArray() ?? Array.Empty<string>();

            var accepted = await _devchain.Engine.NewPayloadV4Async(built.ExecutionPayload, requests, ZeroRoot);
            if (accepted.Status != EnginePayloadStatus.Valid)
                throw new InvalidOperationException($"the authoring devchain rejected its own block: {accepted.ValidationError}");
            var hash = built.ExecutionPayload.BlockHash.HexToByteArray();
            await _devchain.Engine.ForkchoiceUpdatedAsync(StateOf(hash), null);

            return new AuthoredBlock(
                built.ExecutionPayload, requests, hash,
                built.ExecutionPayload.StateRoot.HexToByteArray(),
                (ulong)built.ExecutionPayload.BlockNumber.Value);
        }

        private static string DistinctRecipient(int index) => "0x" + (index + 0x10000).ToString("x").PadLeft(40, '0');

        private ISignedTransaction SignedTransfer(EthECKey sender, int nonce, string recipient)
        {
            var tx = new Transaction1559(
                chainId: _chainId, nonce: nonce, maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(2_000_000_000), gasLimit: new BigInteger(100_000),
                receiverAddress: recipient, amount: new BigInteger(1_000_000_000), data: null, accessList: null);
            var signature = sender.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
            return tx;
        }

        public static ForkchoiceStateV1 StateOf(byte[] head, byte[]? finalized = null) => new ForkchoiceStateV1
        {
            HeadBlockHash = head.ToHex(true),
            SafeBlockHash = head.ToHex(true),
            FinalizedBlockHash = (finalized ?? head).ToHex(true)
        };

        public ValueTask DisposeAsync() => _devchain.DisposeAsync();
    }
}
