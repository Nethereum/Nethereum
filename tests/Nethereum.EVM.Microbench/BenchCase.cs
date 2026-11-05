using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Microbench
{
    public readonly record struct RunOutcome(bool Success, string? Error, long GasUsed);

    public class BenchCase
    {
        public const string Sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public const string Contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public string Name { get; init; } = "";
        public long Operations { get; init; }
        public byte[] Code { get; init; } = Array.Empty<byte>();
        public byte[] Calldata { get; init; } = Array.Empty<byte>();
        public EvmUInt256 ContractBalance { get; init; } = EvmUInt256.Zero;
        public KeyValuePair<string, byte[]>[] OtherContracts { get; init; } = Array.Empty<KeyValuePair<string, byte[]>>();

        public Dictionary<string, AccountState> BuildAccounts()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Sender] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000_000_000), Nonce = 0 },
                [Contract] = new AccountState { Code = Code, Balance = ContractBalance }
            };
            foreach (var other in OtherContracts)
                accounts[other.Key] = new AccountState { Code = other.Value };
            return accounts;
        }

        public virtual Func<Task<RunOutcome>> Prepare(TransactionExecutor executor)
        {
            var context = BuildContext(BuildAccounts());
            return async () =>
            {
                var result = await executor.ExecuteAsync(context);
                return new RunOutcome(result.Success, result.Error, result.GasUsed);
            };
        }

        public TransactionExecutionContext BuildContext(Dictionary<string, AccountState> accounts)
        {
            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = Sender,
                To = Contract,
                Data = Calldata,
                GasLimit = 16_000_000,
                Value = EvmUInt256.Zero,
                GasPrice = 0,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ExecutionState = new ExecutionStateService(new InMemoryStateReader(accounts))
            };
        }
    }
}
