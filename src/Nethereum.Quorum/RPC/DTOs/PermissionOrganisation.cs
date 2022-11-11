using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{

    public class PermissionOrganisation
    {
        /// <summary>
        /// Complete organization ID including all the parent organization IDs separated by.
        /// </summary>
        [JsonProperty(PropertyName = "fullOrgId")]
        public string FullOrgId  { get; set; }

        /// <summary>
        ///level of the organization in the organization hierarchy
        /// </summary>
        [JsonProperty(PropertyName = "level")]
        public int Level { get; set; }

        /// <summary>
        ///Organization ID
        /// </summary>
        [JsonProperty(PropertyName = "orgId")]
        public string OrgId { get; set; }

        /// <summary>
        ///immediate parent organization ID
        /// </summary>
        [JsonProperty(PropertyName = "parentOrgId")]
        public string ParentOrgId { get; set; }

        /// <summary>
        ///master organization under which the organization falls
        /// </summary>
        [JsonProperty(PropertyName = "ultimateParent")]
        public string UltimateParent { get; set; }

        /// <summary>
        ///list of sub-organizations linked to the organization
        /// </summary>
        [JsonProperty(PropertyName = "subOrgList")]
        public string[] SubOrgList { get; set; }

        /// <summary>
        ///organization status
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public int Status { get; set; }
    }

}
