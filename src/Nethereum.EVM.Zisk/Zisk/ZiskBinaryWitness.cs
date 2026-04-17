using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk
{
    public class ZiskBinaryWitness
    {
        public static int Main()
        {
            ZiskIO.WriteLine("BIN:reading");
            var inputBytes = ZiskInput.Read();
            if (inputBytes.Length == 0)
            {
                ZiskIO.WriteLine("BIN:no input");
                ZiskIO.SetOutput(0, 1);
                return 1;
            }

            ZiskIO.Write("BIN:len="); ZiskIO.WriteLong(inputBytes.Length); ZiskIO.Write('\n');

            byte version = inputBytes[0];
            if (version != BinaryBlockWitness.VERSION)
            {
                ZiskIO.WriteLine("BIN:bad version");
                ZiskIO.SetOutput(0, 3);
                return 1;
            }

            var block = BinaryBlockWitness.Deserialize(inputBytes.ToArray());
            block.ProduceBlockCommitments = true;
            block.ComputePostStateRoot = true;

            ZiskIO.Write("BIN:block txs="); ZiskIO.WriteLong(block.Transactions.Count);
            ZiskIO.Write(" accounts="); ZiskIO.WriteLong(block.Accounts.Count); ZiskIO.Write('\n');

            var encoding = RlpBlockEncodingProvider.Instance;
            var registry = MainnetHardforkRegistry.Build(ZiskPrecompileBackends.Instance);
            var stateRootCalc = ResolveStateRootCalculator(block.Features, encoding);
            var isBinary = stateRootCalc is BinaryStateRootCalculator;

            ZiskIO.WriteLine("BIN:exec");

            // NativeAOT devirtualises IStateRootCalculator.ComputeStateRoot inside
            // BlockExecutor to always call Patricia. Work around by computing the
            // binary trie root outside the executor via a direct concrete call.
            block.ComputePostStateRoot = !isBinary;
            var result = Nethereum.EVM.Execution.BlockExecutor.Execute(
                block,
                encoding,
                registry,
                isBinary ? null : stateRootCalc,
                new PatriciaBlockRootCalculator());

            if (isBinary)
            {
                var accounts = Nethereum.EVM.Witness.WitnessStateBuilder.BuildAccountState(block.Accounts);
                var reader = new Nethereum.EVM.BlockchainState.InMemoryStateReader(accounts);
                var es = new Nethereum.EVM.BlockchainState.ExecutionStateService(reader);
                foreach (var addr in accounts.Keys)
                {
                    es.LoadBalanceNonceAndCodeFromStorage(addr);
                    var acct = reader.GetAccountState(addr);
                    if (acct?.Storage != null)
                    {
                        var state = es.CreateOrGetAccountExecutionState(addr);
                        foreach (var s in acct.Storage)
                            state.SetPreStateStorage(s.Key, s.Value);
                    }
                }
                result.StateRoot = ((BinaryStateRootCalculator)stateRootCalc).ComputeStateRoot(es);
            }

            int txFailed = 0;
            foreach (var tx in result.TxResults)
                if (!tx.Success) txFailed++;

            ZiskIO.Write("BIN:executed ok="); ZiskIO.WriteLong(result.TxResults.Count - txFailed);
            ZiskIO.Write(" fail="); ZiskIO.WriteLong(txFailed); ZiskIO.Write('\n');

            ZiskIO.SetOutput(0, txFailed == 0 ? 0u : 1u);
            WriteGas(result.CumulativeGasUsed);
            WriteBytes32ToSlots(3, result.BlockHash ?? new byte[32]);
            WriteBytes32ToSlots(11, result.StateRoot ?? new byte[32]);
            WriteBytes32ToSlots(19, result.TransactionsRoot ?? new byte[32]);
            WriteBytes32ToSlots(27, result.ReceiptsRoot ?? new byte[32]);
            ZiskIO.SetOutput(35, (uint)result.TxResults.Count);

            ZiskIO.Write("BIN:block_hash=");
            WriteHex(result.BlockHash ?? new byte[32]); ZiskIO.Write('\n');
            ZiskIO.Write("BIN:state_root=");
            WriteHex(result.StateRoot ?? new byte[32]); ZiskIO.Write('\n');
            ZiskIO.Write("BIN:OK gas="); ZiskIO.WriteLong(result.CumulativeGasUsed); ZiskIO.Write('\n');

            return 0;
        }

        static void WriteGas(long gas)
        {
            ZiskIO.SetOutput(1, (uint)(gas & 0xFFFFFFFF));
            ZiskIO.SetOutput(2, (uint)((gas >> 32) & 0xFFFFFFFF));
        }

        static void WriteBytes32ToSlots(int startSlot, byte[] data)
        {
            for (int i = 0; i < 8; i++)
            {
                uint word = (uint)(data[i * 4] << 24 | data[i * 4 + 1] << 16 |
                                   data[i * 4 + 2] << 8 | data[i * 4 + 3]);
                ZiskIO.SetOutput(startSlot + i, word);
            }
        }

        static void WriteHex(byte[] data)
        {
            ZiskIO.Write("0x");
            for (int i = 0; i < data.Length; i++)
            {
                var b = data[i];
                ZiskIO.Write((char)HexNibble(b >> 4));
                ZiskIO.Write((char)HexNibble(b & 0xF));
            }
        }

        static int HexNibble(int n) => n < 10 ? '0' + n : 'a' + n - 10;

        static IStateRootCalculator ResolveStateRootCalculator(
            BlockFeatureConfig features, IBlockEncodingProvider encoding)
        {
            if (features != null && features.StateTree == WitnessStateTreeType.Binary)
            {
                IHashProvider hashProvider;
                if (features.HashFunction == WitnessHashFunction.Blake3)
                    hashProvider = new Blake3HashProvider();
                else if (features.HashFunction == WitnessHashFunction.Poseidon)
                    hashProvider = new PoseidonPairHashProvider();
                else if (features.HashFunction == WitnessHashFunction.Sha256)
                    hashProvider = new Sha256HashProvider();
                else
                {
                    ZiskIO.Write("BIN:unknown hash "); ZiskIO.WriteLong((int)features.HashFunction); ZiskIO.Write('\n');
                    ZiskIO.SetOutput(0, 4);
                    return new PatriciaStateRootCalculator(encoding);
                }
                return new BinaryStateRootCalculator(hashProvider);
            }
            return new PatriciaStateRootCalculator(encoding);
        }
    }
}
