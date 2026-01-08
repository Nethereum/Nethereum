using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class CompiledContractSource
    {
        public Guid Id { get; set; }
        public Guid CompilationId { get; set; }
        public byte[] SourceHash { get; set; }
        public string Path { get; set; }
    }
}
