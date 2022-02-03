using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class PermissionAccount
    {
        /// <summary>
        /// Account ID
        /// </summary>
        [DataMember(Name = "acctId")]
        public long AccountId { get; set; }

        /// <summary>
        ///indicates if the account is admin account for the organization
        /// </summary>
        [DataMember(Name = "isOrgAdmin")]
        public bool IsOrgAdmin { get; set; }

        /// <summary>
        ///Organization ID
        /// </summary>
        [DataMember(Name = "orgId")]
        public string OrgId { get; set; }

        /// <summary>
        ///Role assigned to the account
        /// </summary>
        [DataMember(Name = "roleId")]
        public string RoleId { get; set; }

        /// <summary>
        ///Account status
        /// </summary>
        [DataMember(Name = "status")]
        public int Status { get; set; }
    }

}
