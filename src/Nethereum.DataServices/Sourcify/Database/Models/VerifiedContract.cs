using System;

namespace Nethereum.DataServices.Sourcify.Database.Models
{
    public class VerifiedContract
    {
        public long Id { get; set; }
        public Guid DeploymentId { get; set; }
        public Guid CompilationId { get; set; }
        public bool CreationMatch { get; set; }
        public string CreationValues { get; set; }
        public string CreationTransformations { get; set; }
        public bool? CreationMetadataMatch { get; set; }
        public bool RuntimeMatch { get; set; }
        public string RuntimeValues { get; set; }
        public string RuntimeTransformations { get; set; }
        public bool? RuntimeMetadataMatch { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
