namespace Nethereum.EVM.Gas.Intrinsic
{
    public interface ICalldataFloorRule
    {
        long FloorTokensInCalldata(byte[] data);

        long FloorPerTokenGas(byte[] data);
    }
}
