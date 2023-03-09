namespace Nethereum.Unity.Metamask
{
#if !DOTNET35
    using Nethereum.Metamask;

    public class MetamaskWebglHostProvider : MetamaskHostProvider
    {
        public static MetamaskHostProvider CreateOrGetCurrentInstance()
        {
            //instantiation sets the current instance
            if (Current == null) return new MetamaskWebglHostProvider();
            return Current;
        }

        public MetamaskWebglHostProvider() : base(new MetamaskWebglTaskRequestInterop())
        {

        }
    }
#endif
}
