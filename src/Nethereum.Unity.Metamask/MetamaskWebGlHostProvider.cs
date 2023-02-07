namespace Nethereum.Unity.Metamask
{
#if !DOTNET35
    using Nethereum.Metamask;

    public class MetamaskWebGlHostProvider : MetamaskHostProvider
    {
        public static MetamaskHostProvider CreateOrGetCurrentInstance()
        {
            //instantiation sets the current instance
            if (Current == null) return new MetamaskWebGlHostProvider();
            return Current;
        }

        public MetamaskWebGlHostProvider() : base(new MetamaskTaskRequestInterop())
        {

        }
    }
#endif
}
