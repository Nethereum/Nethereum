using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class ServiceGenerator : ClassGeneratorBase<ServiceTemplate, ServiceModel>
    {
        public  ContractABI ContractABI { get; }

        public ServiceGenerator(ContractABI contractABI, string contractName, string byteCode, string @namespace, string cqsNamespace, string functionOutputNamespace)
        {
            ContractABI = contractABI;
            ClassModel = new ServiceModel(contractABI, contractName, byteCode, @namespace, cqsNamespace, functionOutputNamespace);
            ClassTemplate = new ServiceTemplate(ClassModel);
        }
    }
}