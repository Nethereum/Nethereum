namespace Nethereum.Unity
{
    public class IpfsUrlService
    {
        public static string DefaultIpfsGateway = "ipfs.infura.io/ipfs/";
        public static string ResolveIpfsUrlGateway(string url)
        {
            if (url.StartsWith("ipfs:"))
            {
                url = url.Replace("ipfs://", DefaultIpfsGateway);
            }
            return url;
        }
    }
}