using Nethereum.ABI.CompilationMetadata;
using Nethereum.DataServices.Etherscan;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Sourcify.Responses
{

    public class SourcifyContentFile
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }
}
