using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class Code
    {
        public byte[] CodeHash { get; set; }
        public byte[] CodeHashKeccak { get; set; }
        public byte[] CodeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
