

#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyVerifyResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }

#if NET8_0_OR_GREATER
        public JsonElement Result { get; set; }
#else
    public object Result { get; set; }
#endif
    }
}
