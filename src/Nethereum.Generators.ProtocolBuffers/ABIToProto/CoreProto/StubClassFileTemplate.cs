using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto
{
    public class StubClassFileTemplate: ClassFileTemplate
    {
        public StubClassFileTemplate(IClassModel classModel, IClassTemplate classTemplate) : base(classModel, classTemplate)
        {
        }

        public override string GenerateNamespaceDependency(string namespaceName)
        {
            return string.Empty;
        }

        public override string GenerateFullClass()
        {
            return ClassTemplate.GenerateClass();
        }
    }
}