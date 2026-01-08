using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class Source
    {
        public byte[] SourceHash { get; set; }
        public byte[] SourceHashKeccak { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
