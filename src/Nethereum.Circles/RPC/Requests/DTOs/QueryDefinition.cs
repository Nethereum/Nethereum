using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.Circles.RPC.Requests.DTOs
{

    /// <summary>
    /// Represents the query definition for a `circles_query` request.
    /// </summary>
    public class QueryDefinition
    {
        [JsonProperty("Namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonProperty("Table")]
        public string Table { get; set; } = string.Empty;

        [JsonProperty("Columns")]
        public List<string> Columns { get; set; } = new List<string>();

        [JsonProperty("Filter")]
        public List<Filter> Filter { get; set; } = new List<Filter>();

        [JsonProperty("Order")]
        public List<Order> Order { get; set; } = new List<Order>();

        [JsonProperty("Limit")]
        public int Limit { get; set; } = 100;
    }

}
