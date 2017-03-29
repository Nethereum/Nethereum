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

        public ParityDefaultExtraData DefaultExtraData { get; private set; }
        public ParityExtraData ExtraData { get; private set; }
        public ParityGasCeilTarget GasCeilTarget { get; private set; }
        public ParityGasFloorTarget GasFloorTarget { get; private set; }
        public ParityMinGasPrice MinGasPrice { get; private set; }
        public ParityTransactionsLimit TransactionsLimit { get; private set; }
    }
}