using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class CompiledContractSignature
    {
        public Guid Id { get; set; }
        public Guid CompilationId { get; set; }
        public byte[] SignatureHash32 { get; set; }
        public SignatureType SignatureType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
