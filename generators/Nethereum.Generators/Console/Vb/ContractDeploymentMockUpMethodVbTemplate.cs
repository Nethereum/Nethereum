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
                $@"{SpaceUtils.Three____Tabs} ' Deployment 
{SpaceUtils.Three____Tabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(_contractDeploymentCQSMessageModel.ConstructorABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}Dim transactionReceiptDeployment = Await web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAndWaitForReceiptAsync({messageVariableName})
{SpaceUtils.Three____Tabs}Dim contractAddress = transactionReceiptDeployment.ContractAddress
{SpaceUtils.Three____Tabs}";

        }
    }
}