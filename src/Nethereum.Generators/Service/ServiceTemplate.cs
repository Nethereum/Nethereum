using Nethereum.Generators.Core;

namespace Nethereum.Generators.Service
{
    public class ServiceTemplate: IClassTemplate
    {
        private ServiceModel _model;
        private FunctionServiceMethodTemplate _functionServiceMethodTemplate;
        private ContractDeploymentServiceMethodsTemplate _deploymentServiceMethodsTemplate;
        public ServiceTemplate(ServiceModel model)
        {
            _functionServiceMethodTemplate = new FunctionServiceMethodTemplate(model);
            _deploymentServiceMethodsTemplate = new ContractDeploymentServiceMethodsTemplate(model);
            _model = model;
        }

        public string GenerateFullClass()
        {
            return
                $@"{SpaceUtils.NoTabs}using System;
{SpaceUtils.NoTabs}using System.Threading.Tasks;
{SpaceUtils.NoTabs}using System.Numerics;
{SpaceUtils.NoTabs}using Nethereum.Hex.HexTypes;
{SpaceUtils.NoTabs}using Nethereum.ABI.FunctionEncoding.Attributes;
{SpaceUtils.NoTabs}using {_model.CQSNamespace};
{SpaceUtils.NoTabs}using {_model.FunctionOutputNamespace};
{SpaceUtils.NoTabs}namespace {_model.Namespace}
{SpaceUtils.NoTabs}{{
{GenerateClass()}
{SpaceUtils.NoTabs}}}
";
        }

        public string GenerateClass()
        {
            return
                $@"
{SpaceUtils.OneTab}public class {_model.GetTypeName()}
{SpaceUtils.OneTab}{{
{SpaceUtils.OneTab}
{_deploymentServiceMethodsTemplate.GenerateMethods()}
{SpaceUtils.OneTab}
{SpaceUtils.TwoTabs}protected readonly Web3.Web3 Web3{{ get; }}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}protected readonly ContractHandler ContractHandler {{ get; }}
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public {_model.GetTypeName()}(Web3.Web3 web3, string contractAddress)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}Web3 = web3;
{SpaceUtils.ThreeTabs}ContractHandler = web3.Eth.GetContractHandler(contractAddress);
{SpaceUtils.TwoTabs}}}
{SpaceUtils.OneTab}
{_functionServiceMethodTemplate.GenerateMethods()}
{SpaceUtils.OneTab}}}";
           
        }
    }
}