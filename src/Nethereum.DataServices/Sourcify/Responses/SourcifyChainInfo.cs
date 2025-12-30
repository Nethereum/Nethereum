#if NET8_0_OR_GREATER
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyChainInfo
    {
        public string Name { get; set; }
        public string ChainId { get; set; }
        public string Network { get; set; }
        public string Rpc { get; set; }
        public string Explorer { get; set; }
        public bool Supported { get; set; }
    }
}
