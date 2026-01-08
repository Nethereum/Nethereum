using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class Signature
    {
        public byte[] SignatureHash32 { get; set; }
        public byte[] SignatureHash4 { get; set; }
        public string SignatureText { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
