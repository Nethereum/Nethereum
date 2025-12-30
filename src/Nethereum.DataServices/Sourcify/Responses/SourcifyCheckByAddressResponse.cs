

#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyCheckByAddressResponse
    {
        public string Status { get; set; }
        public bool IsVerified { get; set; }

#if NET8_0_OR_GREATER
        public JsonElement Result { get; set; }
#else
    public object Result { get; set; }
#endif
    }
}
