using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class SourcifyMatch
    {
        public long Id { get; set; }
        public long VerifiedContractId { get; set; }
        public string CreationMatch { get; set; }
        public string RuntimeMatch { get; set; }
        public string Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
