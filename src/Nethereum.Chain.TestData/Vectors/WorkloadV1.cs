using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Chain.TestData.Vectors
{
    /// <summary>
    /// The seed sync vector: deterministic balance/nonce activity across the account roster — multiple
    /// senders, multiple txs per block, and an empty block. Accounts are prefunded at genesis by the
    /// driver. Richer content (contract deploys, ERC20 + logs, selfdestruct/clear, withdrawals, a reorg
    /// fork point) is layered on as this vector — and sibling vectors — grow, like the Ethereum vectors.
    /// </summary>
    public sealed class WorkloadV1 : ISyncVector
    {
        public string Name => "workload-v1";
        public int Version => 1;

        public async Task BuildAsync(IVectorChainDriver d)
        {
            var a = d.Accounts;

            d.QueueTransfer(a.Alice, a.Bob.Address, Eth(1));
            d.QueueTransfer(a.Alice, a.Carol.Address, Eth(2));
            await d.ProduceBlockAsync();

            d.QueueTransfer(a.Bob, a.Dave.Address, Eth(1));
            d.QueueTransfer(a.Carol, a.Erin.Address, Eth(1));
            await d.ProduceBlockAsync();

            await d.ProduceBlockAsync();

            d.QueueTransfer(a.Grace, a.Heidi.Address, Eth(5));
            await d.ProduceBlockAsync();

            d.QueueDeploy(a.Frank, WorkloadContracts.StorageLoggerBytecode);
            await d.ProduceBlockAsync();

            var callable = d.QueueDeploy(a.Erin, WorkloadContracts.CallableStorageLoggerBytecode);
            await d.ProduceBlockAsync();

            d.QueueCall(a.Dave, callable, System.Array.Empty<byte>());
            await d.ProduceBlockAsync();

            var reverter = d.QueueDeploy(a.Heidi, WorkloadContracts.RevertOnCallBytecode);
            await d.ProduceBlockAsync();

            d.QueueCall(a.Grace, reverter, System.Array.Empty<byte>());
            await d.ProduceBlockAsync();

            // Block 10 — deploy a contract that writes storage then SELFDESTRUCTs in its constructor.
            // Same-tx create+destroy (EIP-6780): the account is deleted and its storage cleared.
            d.QueueDeploy(a.Bob, WorkloadContracts.SelfDestructInConstructorBytecode);
            await d.ProduceBlockAsync();

            d.QueueDeploy(a.Carol, WorkloadContracts.MultiStorageLoggerBytecode);
            await d.ProduceBlockAsync();

            var callee = d.QueueDeploy(a.Frank, WorkloadContracts.CallableStorageLoggerBytecode);
            await d.ProduceBlockAsync();

            var forwarder = d.QueueDeploy(a.Erin, WorkloadContracts.ForwarderBytecode);
            await d.ProduceBlockAsync();

            var calleeArg = new byte[32];
            callee.HexToByteArray().CopyTo(calleeArg, 12);
            d.QueueCall(a.Heidi, forwarder, calleeArg);
            await d.ProduceBlockAsync();

            var vault = d.QueueDeploy(a.Alice, WorkloadContracts.VaultBytecode);
            await d.ProduceBlockAsync();

            d.QueueTransfer(a.Bob, vault, Eth(5));
            await d.ProduceBlockAsync();

            var delegateForwarder = d.QueueDeploy(a.Carol, WorkloadContracts.DelegateForwarderBytecode);
            await d.ProduceBlockAsync();

            d.QueueCall(a.Dave, delegateForwarder, calleeArg);
            await d.ProduceBlockAsync();

            // Block 19 — deploy an increment contract: ctor sets slot 0 = 0x2a; every CALL does
            // SSTORE(0, SLOAD(0)+1) — a READ-MODIFY-WRITE of an existing storage slot (ERC20-balance shape).
            var counter = d.QueueDeploy(a.Frank, WorkloadContracts.IncrementOnCallBytecode);
            await d.ProduceBlockAsync();

            d.QueueCall(a.Alice, counter, System.Array.Empty<byte>());
            await d.ProduceBlockAsync();

            d.QueueCall(a.Bob, counter, System.Array.Empty<byte>());
            await d.ProduceBlockAsync();

            for (var round = 0; round < BulkRounds; round++)
            {
                var from = a.All[round % a.All.Count];
                d.QueueTransfer(from, a.All[(round + 1) % a.All.Count].Address, Eth(1));

                var freshEoa = "0x" + (0x1000 + round).ToString("x").PadLeft(40, '0');
                d.QueueTransfer(from, freshEoa, Eth(1));

                if (round % 3 == 0) d.QueueDeploy(from, WorkloadContracts.StorageLoggerBytecode);
                if (round % 5 == 0) d.QueueDeploy(from, WorkloadContracts.MultiStorageLoggerBytecode);

                await d.ProduceBlockAsync();
            }
        }

        public const int BulkRounds = 29;

        private static BigInteger Eth(long whole) => new BigInteger(whole) * BigInteger.Pow(10, 18);
    }
}
