using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class BlockAuthoringApiService : RpcClientWrapper, IBlockAuthoringApiService
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

        public IParityDefaultExtraData DefaultExtraData { get; }
        public IParityExtraData ExtraData { get; }
        public IParityGasCeilTarget GasCeilTarget { get; }
        public IParityGasFloorTarget GasFloorTarget { get; }
        public IParityMinGasPrice MinGasPrice { get; }
        public IParityTransactionsLimit TransactionsLimit { get; }
    }
}