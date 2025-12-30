

#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyMetadataResponse
    {
        public string Status { get; set; }
        public string StorageTimestamp { get; set; }
        public string Error { get; set; }

#if NET8_0_OR_GREATER
      public JsonElement Metadata { get; set; }
#else
      public object Metadata { get; set; }
#endif
    }
}
