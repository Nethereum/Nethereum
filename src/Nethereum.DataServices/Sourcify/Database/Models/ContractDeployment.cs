using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class ContractDeployment
    {
        public Guid Id { get; set; }
        public long ChainId { get; set; }
        public byte[] Address { get; set; }
        public byte[] TransactionHash { get; set; }
        public decimal? BlockNumber { get; set; }
        public decimal? TransactionIndex { get; set; }
        public byte[] Deployer { get; set; }
        public Guid ContractId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
