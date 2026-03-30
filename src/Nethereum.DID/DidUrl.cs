using System.Collections.Generic;

namespace Nethereum.DID
{
    public class DidUrl
    {
        public string Did { get; set; }
        public string Url { get; set; }
        public string Method { get; set; }
        public string Id { get; set; }
        public string Path { get; set; }
        public string Fragment { get; set; }
        public string Query { get; set; }
        public Dictionary<string, string> Params { get; set; }

        public DidUrl()
        {
            Params = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return Url ?? Did ?? string.Empty;
        }
    }
}
