using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class CompiledContract
    {
        public Guid Id { get; set; }
        public string Compiler { get; set; }
        public string Version { get; set; }
        public string Language { get; set; }
        public string Name { get; set; }
        public string FullyQualifiedName { get; set; }
        public string CompilerSettings { get; set; }
        public string CompilationArtifacts { get; set; }
        public byte[] CreationCodeHash { get; set; }
        public string CreationCodeArtifacts { get; set; }
        public byte[] RuntimeCodeHash { get; set; }
        public string RuntimeCodeArtifacts { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
