using Nethereum.CoreChain;
using Nethereum.EVM.Witness;
using Nethereum.Model;
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

            ZiskIO.WriteLine("BIN:exec");
            var encoding = RlpBlockEncodingProvider.Instance;
            var registry = MainnetHardforkRegistry.Build(ZiskPrecompileBackends.Instance);
            // block.Features.Fork is set by BinaryBlockWitness.Deserialize; it rejects
            // Unspecified at the wire layer, so no fallback is needed here.
            var result = Nethereum.EVM.Execution.BlockExecutor.Execute(
                block,
                encoding,
                registry,
                new PatriciaStateRootCalculator(encoding),
                new PatriciaBlockRootCalculator());

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
            ZiskIO.SetOutput(35, (uint)block.Transactions.Count);

            ZiskIO.Write("BIN:block_hash="); WriteHex(result.BlockHash ?? new byte[32]); ZiskIO.Write('\n');
            ZiskIO.Write("BIN:state_root="); WriteHex(result.StateRoot ?? new byte[32]); ZiskIO.Write('\n');
            ZiskIO.Write("BIN:OK gas="); ZiskIO.WriteLong(result.CumulativeGasUsed); ZiskIO.Write('\n');

            return txFailed == 0 ? 0 : 1;
        }

        static void WriteGas(long gasUsed)
        {
            ZiskIO.SetOutput(1, (uint)(gasUsed & 0xFFFFFFFF));
            ZiskIO.SetOutput(2, (uint)((gasUsed >> 32) & 0xFFFFFFFF));
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
    }
}
