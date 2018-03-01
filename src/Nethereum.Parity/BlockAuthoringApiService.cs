using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class BlockAuthoringApiService : RpcClientWrapper
    {
        public BlockAuthoringApiService(IClient client) : base(client)
        {
            DefaultExtraData = new ParityDefaultExtraData(client);
            ExtraData = new ParityExtraData(client);
            GasFloorTarget = new ParityGasFloorTarget(client);
            GasCeilTarget = new ParityGasCeilTarget(client);
            MinGasPrice = new ParityMinGasPrice(client);
            TransactionsLimit = new ParityTransactionsLimit(client);
        }

        public ParityDefaultExtraData DefaultExtraData { get; }
        public ParityExtraData ExtraData { get; }
        public ParityGasCeilTarget GasCeilTarget { get; }
        public ParityGasFloorTarget GasFloorTarget { get; }
        public ParityMinGasPrice MinGasPrice { get; }
        public ParityTransactionsLimit TransactionsLimit { get; }
    }
}