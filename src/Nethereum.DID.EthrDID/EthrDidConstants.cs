using System.Collections.Generic;

namespace Nethereum.DID.EthrDID
{
    public static class EthrDidConstants
    {
        public const string MethodName = "ethr";

        public const string MainnetRegistryAddress = "0xdca7ef03e98e0dc2b855be647c39abe984fcf21b";
        public const string SepoliaRegistryAddress = "0x03d5003bf0e79C5F5223588F347ebA39AfbC3818";
        public const string PolygonRegistryAddress = "0xdca7ef03e98e0dc2b855be647c39abe984fcf21b";

        public const string DelegateTypeVeriKey = "veriKey";
        public const string DelegateTypeSignAuth = "sigAuth";

        public const string AttributePubKeyPrefix = "did/pub/";
        public const string AttributeServicePrefix = "did/svc/";

        public static Dictionary<long, string> GetDefaultRegistryAddresses()
        {
            return new Dictionary<long, string>
            {
                { 1, MainnetRegistryAddress },
                { 11155111, SepoliaRegistryAddress },
                { 137, PolygonRegistryAddress },
                { 80001, PolygonRegistryAddress }
            };
        }
    }
}
