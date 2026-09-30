using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.SyncValidator;

// State acceptance battery: byte-compares eth_getProof (inclusion AND exclusion), balance, nonce
// and code between a reference node and ours at one block. A proof exercises the full trie path
// from the state root to the leaf, so agreement here is end-to-end evidence that the synced
// state — trie keying, node content, flat values, bytecode — matches the canonical chain.
// Random sampling is seeded from the block number so reruns compare like-for-like.
internal static class StateAcceptance
{
    private static readonly string[] DefaultAccounts =
    {
        "0xab5801a7d398351b8be11c439e05c5b3259aec9b", // long-lived EOA fixture
        "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", // long-lived contract-with-storage fixture (USDC)
    };

    public static async Task<int> RunAsync(
        IWeb3 reference, IWeb3 ours, long block, string[]? accounts, int samples, CancellationToken ct)
    {
        accounts = accounts is { Length: > 0 } ? accounts : DefaultAccounts;
        var bp = new BlockParameter(new HexBigInteger(block));
        var refProof = new EthGetProof(reference.Client);
        var ourProof = new EthGetProof(ours.Client);

        // Early fixed slots hit real storage on most contracts; the seeded-random slots are
        // usually exclusion proofs — absence must be provable and byte-equal too.
        var rnd = new Random((int)(block % int.MaxValue));
        var slots = new[] { Slot(0), Slot(1), Slot(2), Slot(3), RandomHex32(rnd), RandomHex32(rnd) };

        Console.WriteLine($"state-acceptance at block {block:N0}: {accounts.Length} fixture account(s) + {samples} sampled address(es), {slots.Length} storage slots each");
        int failedAccounts = 0, checksRun = 0;

        foreach (var acct in accounts)
            if (!await CheckAccountAsync(acct)) failedAccounts++;

        for (int i = 0; i < samples && !ct.IsCancellationRequested; i++)
            if (!await CheckAccountAsync(RandomAddress(rnd))) failedAccounts++;

        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"STATE-ACCEPTANCE {(failedAccounts == 0 ? "PASS" : "FAIL")}: accounts-checked={accounts.Length + samples} checks={checksRun} mismatching-accounts={failedAccounts} block={block:N0}");
        return failedAccounts == 0 ? 0 : 1;

        async Task<bool> CheckAccountAsync(string address)
        {
            ct.ThrowIfCancellationRequested();
            var diffs = new List<string>();
            try
            {
                var pr = await refProof.SendRequestAsync(address, slots, bp);
                var po = await ourProof.SendRequestAsync(address, slots, bp);
                diffs.AddRange(DtoComparer.Compare(pr, po, $"proof[{address}]"));

                var br = await reference.Eth.GetBalance.SendRequestAsync(address, bp);
                var bo = await ours.Eth.GetBalance.SendRequestAsync(address, bp);
                diffs.AddRange(DtoComparer.Compare(br, bo, $"balance[{address}]"));

                var nr = await reference.Eth.Transactions.GetTransactionCount.SendRequestAsync(address, bp);
                var no = await ours.Eth.Transactions.GetTransactionCount.SendRequestAsync(address, bp);
                diffs.AddRange(DtoComparer.Compare(nr, no, $"nonce[{address}]"));

                var cr = await reference.Eth.GetCode.SendRequestAsync(address, bp);
                var co = await ours.Eth.GetCode.SendRequestAsync(address, bp);
                diffs.AddRange(DtoComparer.Compare(cr, co, $"code[{address}]"));

                checksRun += 4;
            }
            catch (Exception ex)
            {
                diffs.Add($"rpc[{address}]: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
            }

            if (diffs.Count == 0)
            {
                Console.WriteLine($"  ok   {address} (proof+balance+nonce+code)");
                return true;
            }

            Console.WriteLine($"  FAIL {address} ({diffs.Count} differing fields):");
            foreach (var d in diffs.Take(20)) Console.WriteLine("    " + d);
            if (diffs.Count > 20) Console.WriteLine($"    ... and {diffs.Count - 20} more");
            return false;
        }
    }

    private static string Slot(int n) => "0x" + n.ToString("x64");

    private static string RandomHex32(Random rnd)
    {
        var b = new byte[32];
        rnd.NextBytes(b);
        return "0x" + Convert.ToHexString(b).ToLowerInvariant();
    }

    private static string RandomAddress(Random rnd)
    {
        var b = new byte[20];
        rnd.NextBytes(b);
        return "0x" + Convert.ToHexString(b).ToLowerInvariant();
    }
}
