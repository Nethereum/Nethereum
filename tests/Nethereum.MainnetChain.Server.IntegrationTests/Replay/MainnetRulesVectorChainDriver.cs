using System.Numerics;
using Nethereum.Chain.TestData;
using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.MainnetChain.Server.IntegrationTests.Replay;

public sealed record StoredReplayBlock(
    BlockHeader Header,
    byte[] Hash,
    IReadOnlyList<ISignedTransaction> Transactions,
    IList<Withdrawal> Withdrawals);

public sealed class MainnetRulesVectorChainDriver : IVectorChainDriver
{
    private const long SecondsPerBlock = 12;
    private static readonly BigInteger MainnetChainId = 1;

    private readonly CoreChain.BlockProducer _producer;
    private readonly Dictionary<string, int> _nonces = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ISignedTransaction> _pending = new();
    private readonly List<StoredReplayBlock> _produced = new();
    private long _parentTimestamp;

    public MainnetRulesVectorChainDriver(CoreChain.BlockProducer producer, long headTimestamp)
    {
        _producer = producer;
        _parentTimestamp = headTimestamp;
    }

    public WorkloadAccounts Accounts { get; } = new();

    public IReadOnlyList<StoredReplayBlock> Produced => _produced;

    public void QueueTransfer(RosterAccount from, string to, BigInteger valueWei)
        => Queue(from, to, valueWei, null, 21_000);

    public string QueueDeploy(RosterAccount from, byte[] bytecode)
    {
        var nonce = _nonces.TryGetValue(from.Address, out var current) ? current : 0;
        Queue(from, null, BigInteger.Zero, bytecode, 1_000_000);
        return Nethereum.Util.ContractUtils.CalculateContractAddress(from.Address, nonce);
    }

    public void QueueCall(RosterAccount from, string to, byte[] data)
        => Queue(from, to, BigInteger.Zero, data, 200_000);

    public async Task<ProducedBlock> ProduceBlockAsync()
    {
        var transactions = _pending.ToList();
        _pending.Clear();
        _parentTimestamp += SecondsPerBlock;
        var withdrawals = new List<Withdrawal>();

        var result = await _producer.ProduceBlockAsync(transactions, new BlockProductionOptions
        {
            Timestamp = _parentTimestamp,
            Coinbase = "0x" + new string('c', 40),
            BlockGasLimit = 30_000_000,
            Difficulty = BigInteger.Zero,
            PrevRandao = new byte[32],
            ChainId = MainnetChainId,
            ParentBeaconBlockRoot = new byte[32],
            HardforkName = Nethereum.EVM.HardforkName.Prague,
            Withdrawals = withdrawals,
        });

        _produced.Add(new StoredReplayBlock(result.Header, result.BlockHash, result.IncludedTransactions, withdrawals));
        return new ProducedBlock(
            (long)result.Header.BlockNumber, result.BlockHash, result.Header.StateRoot,
            result.Header.TransactionsHash, result.Header.ReceiptHash,
            result.IncludedTransactions.Count, 0);
    }

    private void Queue(RosterAccount from, string? to, BigInteger value, byte[]? data, BigInteger gasLimit)
    {
        _nonces.TryGetValue(from.Address, out var nonce);
        _nonces[from.Address] = nonce + 1;
        var tx = new Transaction1559(
            chainId: MainnetChainId,
            nonce: nonce,
            maxPriorityFeePerGas: BigInteger.Zero,
            maxFeePerGas: new BigInteger(2_000_000_000),
            gasLimit: gasLimit,
            receiverAddress: to,
            amount: value,
            data: data == null || data.Length == 0 ? null : data.ToHex(true),
            accessList: null);
        var signature = from.Key.SignAndCalculateYParityV(tx.RawHash);
        tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
        _pending.Add(tx);
    }
}
