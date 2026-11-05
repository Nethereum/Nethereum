using Nethereum.Parity.RPC.BlockAuthoring;

namespace Nethereum.Parity
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "IBlockAuthoringApiService")]
    public interface IBlockAuthoringApiService
    {
        IParityDefaultExtraData DefaultExtraData { get; }
        IParityExtraData ExtraData { get; }
        IParityGasCeilTarget GasCeilTarget { get; }
        IParityGasFloorTarget GasFloorTarget { get; }
        IParityMinGasPrice MinGasPrice { get; }
        IParityTransactionsLimit TransactionsLimit { get; }
    }
}