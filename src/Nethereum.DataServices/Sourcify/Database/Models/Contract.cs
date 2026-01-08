using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class Contract
    {
        public Guid Id { get; set; }
        public byte[] CreationCodeHash { get; set; }
        public byte[] RuntimeCodeHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
