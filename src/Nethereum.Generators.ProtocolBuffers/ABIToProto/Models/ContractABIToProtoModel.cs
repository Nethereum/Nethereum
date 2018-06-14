using Nethereum.Generators.Core;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators;

namespace Nethereum.Generators.ProtocolBuffers.ABIToProto.Models
{
    public class ContractABIToProtoModel: TypeMessageModel
    {
        public ConstructorABIToProtoGenerator ConstructorGenerator { get; }
        public FunctionABIToProtoGenerator[] FunctionGenerators { get; }
        public EventABIToProtoGenerator[] EventGenerators { get; }

        public ContractABIToProtoModel(string name, string classNameSuffix, ConstructorABIToProtoGenerator constructorGenerator, FunctionABIToProtoGenerator[] functionGenerators, EventABIToProtoGenerator[] eventGenerators) : base("", name, classNameSuffix)
        {
            ConstructorGenerator = constructorGenerator;
            FunctionGenerators = functionGenerators;
            EventGenerators = eventGenerators;
            CodeGenLanguage = CodeGenLanguage.Proto;
        }
    }
}