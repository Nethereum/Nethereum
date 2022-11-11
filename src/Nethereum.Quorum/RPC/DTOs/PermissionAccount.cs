using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class PermissionAccount
    {
        /// <summary>
        /// Account ID
        /// </summary>
        [JsonProperty(PropertyName = "acctId")]
        public long AccountId { get; set; }

        /// <summary>
        ///indicates if the account is admin account for the organization
        /// </summary>
        [JsonProperty(PropertyName = "isOrgAdmin")]
        public bool IsOrgAdmin { get; set; }

        /// <summary>
        ///Organization ID
        /// </summary>
        [JsonProperty(PropertyName = "orgId")]
        public string OrgId { get; set; }

        /// <summary>
        ///Role assigned to the account
        /// </summary>
        [JsonProperty(PropertyName = "roleId")]
        public string RoleId { get; set; }

        /// <summary>
        ///Account status
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public int Status { get; set; }
    }

}
