using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.Console.Vb
{
    public class ContractDeploymentMockUpMethodVbTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtoVbTemplate;

        public ContractDeploymentMockUpMethodVbTemplate(ContractDeploymentCQSMessageModel contractDeploymentCQSMessageModel)
        {
            _contractDeploymentCQSMessageModel = contractDeploymentCQSMessageModel;
            _parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
        }

        public string GenerateMethods()
        {
            var messageType = _contractDeploymentCQSMessageModel.GetTypeName();
            var messageVariableName = _contractDeploymentCQSMessageModel.GetVariableName();

            return
                $@"{SpaceUtils.ThreeTabs} ' Deployment 
{SpaceUtils.ThreeTabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(_contractDeploymentCQSMessageModel.ConstructorABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}Dim transactionReceiptDeployment = Await web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAndWaitForReceiptAsync({messageVariableName})
{SpaceUtils.ThreeTabs}Dim contractAddress = transactionReceiptDeployment.ContractAddress
{SpaceUtils.ThreeTabs}";

        }
    }
}