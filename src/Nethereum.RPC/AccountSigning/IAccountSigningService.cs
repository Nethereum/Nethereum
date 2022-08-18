namespace Nethereum.RPC.AccountSigning
{
    public interface IAccountSigningService
    {
        IEthSignTypedDataV4 SignTypedDataV4 { get; }
        IEthPersonalSign PersonalSign { get; }
    }
}
