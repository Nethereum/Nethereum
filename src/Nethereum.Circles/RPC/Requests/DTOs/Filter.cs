using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.Circles.RPC.Requests.DTOs
{
    public abstract class Filter
    {
        [JsonProperty("Type")]
        public string Type { get; set; } = "FilterPredicate";
    }

    public class FilterPredicate : Filter
    {
        [JsonProperty("FilterType")]
        public string FilterType { get; set; } = string.Empty;

        [JsonProperty("Column")]
        public string Column { get; set; } = string.Empty;

        [JsonProperty("Value")]
        public object Value { get; set; }
    }

    public class Conjunction : Filter
    {
        [JsonProperty("ConjunctionType")]
        public string ConjunctionType { get; set; } = "And";

        [JsonProperty("Predicates")]
        public List<Filter> Predicates { get; set; } = new List<Filter>();

        public Conjunction()
        {
            Type = "Conjunction";
        }
    }


    public class Order
    {
        [JsonProperty("Column")]
        public string Column { get; set; } = string.Empty;

        [JsonProperty("SortOrder")]
        public string SortOrder { get; set; } = "ASC";
    }
}
