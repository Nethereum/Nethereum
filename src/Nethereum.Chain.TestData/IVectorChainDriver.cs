using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Chain.TestData
{
    public sealed record ProducedBlock(
        long Number,
        byte[] Hash,
        byte[] StateRoot,
        byte[] TransactionsRoot,
        byte[] ReceiptsRoot,
        int TxCount,
        int LogCount);

    public interface IVectorChainDriver
    {
        WorkloadAccounts Accounts { get; }
        void QueueTransfer(RosterAccount from, string to, BigInteger valueWei);

        string QueueDeploy(RosterAccount from, byte[] bytecode);

        void QueueCall(RosterAccount from, string to, byte[] data);

        Task<ProducedBlock> ProduceBlockAsync();
    }
}
