using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Util;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerHistoryStore — read-only history-tier adapter over the freezer")]
    public sealed class FreezerHistoryStore : IBlockStore, ITransactionStore, IReceiptStore, IBlockAccessListStore,
        IUncleStore, IWithdrawalStore
    {
        private readonly FreezerCore _freezer;
        private readonly FreezerCodecSet _codecs;
        private readonly ReceiptFieldDeriver _deriver;
        private readonly ITransactionVerificationAndRecovery _signer;
        private readonly IRandomKeyIndexStore _index;
        private readonly DecodedClusterCache _cache;

        public FreezerHistoryStore(
            FreezerCore freezer,
            FreezerCodecSet codecs,
            ReceiptFieldDeriver deriver,
            ITransactionVerificationAndRecovery signer,
            IRandomKeyIndexStore hashIndexes,
            DecodedClusterCache cache)
        {
            _freezer = freezer ?? throw new ArgumentNullException(nameof(freezer));
            _codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
            _deriver = deriver ?? throw new ArgumentNullException(nameof(deriver));
            _signer = signer ?? throw new ArgumentNullException(nameof(signer));
            _index = hashIndexes ?? throw new ArgumentNullException(nameof(hashIndexes));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        private const string AppendOnlyMessage = "freezer history is append-only; write via FreezerPromotionService";


        public Task<BlockHeader> GetByNumberAsync(BigInteger number) =>
            Task.FromResult(ReadHeader((long)number));

        public Task<BlockHeader> GetByHashAsync(byte[] hash) =>
            Task.FromResult(TryResolveBlockNumber(hash, out var number) ? ReadHeader(number) : null);

        public Task<BlockHeader> GetLatestAsync() =>
            Task.FromResult(ReadHeader(_freezer.Items - 1));

        public Task<BigInteger> GetHeightAsync() =>
            Task.FromResult((BigInteger)(_freezer.Items - 1));

        public Task SaveAsync(BlockHeader header, byte[] blockHash) =>
            throw new InvalidOperationException(AppendOnlyMessage);

        public Task<bool> ExistsAsync(byte[] hash) =>
            Task.FromResult(TryResolveBlockNumber(hash, out _));

        public Task<byte[]> GetHashByNumberAsync(BigInteger number) =>
            Task.FromResult(ReadHash((long)number));

        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) =>
            EnsureMutable((long)blockNumber);

        public Task DeleteByNumberAsync(BigInteger blockNumber) =>
            EnsureMutable((long)blockNumber);


        Task<ISignedTransaction> ITransactionStore.GetByHashAsync(byte[] txHash) =>
            Task.FromResult(TxAtLocation(txHash));

        public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash) =>
            Task.FromResult(TryResolveBlockNumber(blockHash, out var number)
                ? TxsAt(number)
                : new List<ISignedTransaction>());

        public Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash) =>
            Task.FromResult(TryResolveBlockNumber(blockHash, out var number)
                ? TxsAt(number).Select(tx => tx.Hash).ToList()
                : new List<byte[]>());

        public Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber) =>
            Task.FromResult(TxsAt((long)blockNumber));

        public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber) =>
            throw new InvalidOperationException(AppendOnlyMessage);

        public Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            if (!_index.TryGetTxLocation(txHash, out var blockNumber, out var txIndex))
                return Task.FromResult<TransactionLocation>(null);

            return Task.FromResult(new TransactionLocation
            {
                BlockHash = ReadHash(blockNumber),
                BlockNumber = blockNumber,
                TransactionIndex = txIndex,
            });
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber) =>
            EnsureMutable((long)blockNumber);


        public Task<Receipt> GetByTxHashAsync(byte[] txHash) =>
            Task.FromResult(ReceiptAtLocation(txHash) is { } derived ? ToConsensusReceipt(derived) : null);

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
        {
            if (!_index.TryGetTxLocation(txHash, out var blockNumber, out var txIndex))
                return Task.FromResult<ReceiptInfo>(null);

            var derived = ReceiptAt(blockNumber, txIndex);
            return Task.FromResult(derived == null ? null : ToReceiptInfo(derived, ReadHash(blockNumber), blockNumber, txIndex));
        }

        Task<List<Receipt>> IReceiptStore.GetByBlockHashAsync(byte[] blockHash) =>
            Task.FromResult(TryResolveBlockNumber(blockHash, out var number) ? ReceiptsAt(number) : new List<Receipt>());

        Task<List<Receipt>> IReceiptStore.GetByBlockNumberAsync(BigInteger blockNumber) =>
            Task.FromResult(ReceiptsAt((long)blockNumber));

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex,
            BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice) =>
            throw new InvalidOperationException(AppendOnlyMessage);


        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp) =>
            throw new InvalidOperationException(AppendOnlyMessage);

        Task<byte[]> IBlockAccessListStore.GetByBlockHashAsync(byte[] blockHash) =>
            Task.FromResult(TryResolveBlockNumber(blockHash, out var number) ? ReadBal(number) : null);

        Task<byte[]> IBlockAccessListStore.GetByBlockNumberAsync(BigInteger blockNumber) =>
            Task.FromResult(ReadBal((long)blockNumber));

        public Task DeleteByBlockHashAsync(byte[] blockHash) =>
            TryResolveBlockNumber(blockHash, out var number)
                ? EnsureMutable(number)
                : Task.CompletedTask;


        public Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles) =>
            throw new InvalidOperationException(AppendOnlyMessage);

        Task<IList<BlockHeader>> IUncleStore.GetByBlockHashAsync(byte[] blockHash) =>
            Task.FromResult(TryResolveBlockNumber(blockHash, out var number) ? (IList<BlockHeader>)UnclesAt(number) : null);

        Task<IList<BlockHeader>> IUncleStore.GetByBlockNumberAsync(BigInteger blockNumber) =>
            Task.FromResult((IList<BlockHeader>)UnclesAt((long)blockNumber));


        public Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals) =>
            throw new InvalidOperationException(AppendOnlyMessage);

        Task<IList<Withdrawal>> IWithdrawalStore.GetByBlockHashAsync(byte[] blockHash) =>
            Task.FromResult(TryResolveBlockNumber(blockHash, out var number) ? (IList<Withdrawal>)WithdrawalsAt(number) : null);

        Task<IList<Withdrawal>> IWithdrawalStore.GetByBlockNumberAsync(BigInteger blockNumber) =>
            Task.FromResult((IList<Withdrawal>)WithdrawalsAt((long)blockNumber));


        private bool TryResolveBlockNumber(byte[] hash, out long number) => _index.TryGetBlockNumberByHash(hash, out number);

        private BlockHeader ReadHeader(long number) =>
            _codecs.Headers.Decode(_freezer.ReadCluster(number).Header);

        private byte[] ReadHash(long number) =>
            _codecs.Hashes.Decode(_freezer.ReadCluster(number).Hash);

        private byte[] ReadBal(long number)
        {
            var decoded = _codecs.Bals.Decode(_freezer.ReadCluster(number).Bal);
            return decoded.Length == 0 ? null : decoded;
        }

        private List<ISignedTransaction> TxsAt(long number) => DecodedClusterAt(number).Body.Txs.ToList();

        private List<Receipt> ReceiptsAt(long number) => DecodedClusterAt(number).Receipts.Select(ToConsensusReceipt).ToList();

        private List<BlockHeader> UnclesAt(long number) => DecodedClusterAt(number).Body.Uncles.ToList();

        private List<Withdrawal> WithdrawalsAt(long number) => DecodedClusterAt(number).Body.Withdrawals?.ToList();

        private ISignedTransaction TxAtLocation(byte[] txHash)
        {
            if (!_index.TryGetTxLocation(txHash, out var blockNumber, out var txIndex))
                return null;

            var txs = DecodedClusterAt(blockNumber).Body.Txs;
            return txIndex >= 0 && txIndex < txs.Count ? txs[txIndex] : null;
        }

        private DerivedReceipt ReceiptAtLocation(byte[] txHash)
        {
            return _index.TryGetTxLocation(txHash, out var blockNumber, out var txIndex)
                ? ReceiptAt(blockNumber, txIndex)
                : null;
        }

        private DerivedReceipt ReceiptAt(long blockNumber, int txIndex)
        {
            var receipts = DecodedClusterAt(blockNumber).Receipts;
            return txIndex >= 0 && txIndex < receipts.Count ? receipts[txIndex] : null;
        }

        private DecodedCluster DecodedClusterAt(long number) =>
            _cache.GetOrAdd(number, BuildDecodedCluster);

        private DecodedCluster BuildDecodedCluster(long number)
        {
            var cluster = _freezer.ReadCluster(number);
            var header = _codecs.Headers.Decode(cluster.Header);
            var body = _codecs.Bodies.Decode(cluster.Body);
            var stored = _codecs.Receipts.Decode(cluster.Receipts);

            var derived = _deriver.Derive(header, body, stored);
            var senders = body.Txs.Select(tx => _signer.GetSenderAddress(tx)).ToList();
            var contractAddresses = derived.Select(r => r.ContractAddress).ToList();

            return new DecodedCluster(body, derived, senders, contractAddresses);
        }


        private static Receipt ToConsensusReceipt(DerivedReceipt derived) => new()
        {
            PostStateOrStatus = derived.PostStateOrStatus,
            CumulativeGasUsed = EvmUInt256BigIntegerExtensions.FromBigInteger(derived.CumulativeGasUsed),
            Bloom = derived.Bloom,
            Logs = derived.Logs.Select(l => l.Log).ToList(),
            TransactionType = (byte)derived.TransactionType,
            ContractAddress = derived.ContractAddress,
        };

        private static ReceiptInfo ToReceiptInfo(DerivedReceipt derived, byte[] blockHash, BigInteger blockNumber, int txIndex) => new()
        {
            Receipt = ToConsensusReceipt(derived),
            TxHash = derived.TxHash,
            BlockHash = blockHash,
            BlockNumber = blockNumber,
            TransactionIndex = txIndex,
            GasUsed = derived.GasUsed,
            ContractAddress = derived.ContractAddress,
            EffectiveGasPrice = derived.EffectiveGasPrice.ToBigInteger(),
        };


        private Task EnsureMutable(long blockNumber)
        {
            if (blockNumber < _freezer.Items)
                throw new FreezerImmutableException(
                    $"cannot mutate frozen block {blockNumber}: freezer head is {_freezer.Items}");

            return Task.CompletedTask;
        }
    }
}
