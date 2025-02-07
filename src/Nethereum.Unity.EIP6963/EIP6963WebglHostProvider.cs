namespace Nethereum.Unity.EIP6963
{
#if !DOTNET35
    using Nethereum.EIP6963WalletInterop;

    public class EIP6963WebglHostProvider : EIP6963WalletHostProvider
    {
        public static EIP6963WalletHostProvider CreateOrGetCurrentInstance()
        {
            //instantiation sets the current instance
            if (Current == null) return new EIP6963WebglHostProvider();
            return Current;
        }

        public EIP6963WebglHostProvider() : base(new EIP6963WebglTaskRequestInterop())
        {
            ((EIP6963WebglTaskRequestInterop)this._walletInterop).InitEIP6963();
        }
    }
#endif
}
