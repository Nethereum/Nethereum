using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class PermissionOrganisation
    {
        /// <summary>
        /// Complete organization ID including all the parent organization IDs separated by.
        /// </summary>
        [DataMember(Name = "fullOrgId")]
        public string FullOrgId  { get; set; }

        /// <summary>
        ///level of the organization in the organization hierarchy
        /// </summary>
        [DataMember(Name = "level")]
        public int Level { get; set; }

        /// <summary>
        ///Organization ID
        /// </summary>
        [DataMember(Name = "orgId")]
        public string OrgId { get; set; }

        /// <summary>
        ///immediate parent organization ID
        /// </summary>
        [DataMember(Name = "parentOrgId")]
        public string ParentOrgId { get; set; }

        /// <summary>
        ///master organization under which the organization falls
        /// </summary>
        [DataMember(Name = "ultimateParent")]
        public string UltimateParent { get; set; }

        /// <summary>
        ///list of sub-organizations linked to the organization
        /// </summary>
        [DataMember(Name = "subOrgList")]
        public string[] SubOrgList { get; set; }

        /// <summary>
        ///organization status
        /// </summary>
        [DataMember(Name = "status")]
        public int Status { get; set; }
    }

}
