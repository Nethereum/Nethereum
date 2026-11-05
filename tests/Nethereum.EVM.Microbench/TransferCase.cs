using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

namespace Nethereum.EVM.Microbench
{
    public sealed class TransferCase : BenchCase
    {
        private const int Transfers = 400;
        private readonly bool _distinctReceivers;

        private TransferCase(string name, bool distinctReceivers)
        {
            Name = name;
            Operations = Transfers;
            _distinctReceivers = distinctReceivers;
        }

        public static IEnumerable<BenchCase> All()
        {
            yield return new TransferCase("ETHER TRANSFER tx to new accounts", true);
            yield return new TransferCase("ETHER TRANSFER tx to one account", false);
        }

        private static string Receiver(int index) => "0x" + (0x100000 + index).ToString("x40");

        public override Func<Task<RunOutcome>> Prepare(TransactionExecutor executor)
        {
            var reader = new InMemoryStateReader(BuildAccounts());
            return async () =>
            {
                long gas = 0;
                for (int i = 0; i < Transfers; i++)
                {
                    var state = new ExecutionStateService(reader);
                    var context = BuildContext(reader, state, i);
                    var result = await executor.ExecuteAsync(context);
                    if (!result.Success) return new RunOutcome(false, result.Error, gas);
                    reader.CommitChanges(state);
                    gas += result.GasUsed;
                }
                return new RunOutcome(true, null, gas);
            };
        }

        private TransactionExecutionContext BuildContext(InMemoryStateReader reader, ExecutionStateService state, int index)
        {
            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = Sender,
                To = Receiver(_distinctReceivers ? index : 0),
                Data = Array.Empty<byte>(),
                Nonce = index,
                GasLimit = 21000,
                Value = EvmUInt256.One,
                GasPrice = 0,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ExecutionState = state
            };
        }
    }
}
