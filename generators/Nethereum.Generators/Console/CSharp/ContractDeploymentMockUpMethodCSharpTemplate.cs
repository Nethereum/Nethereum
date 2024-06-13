using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.Console.CSharp
{
    public class ContractDeploymentMockUpMethodCSharpTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;

        public ContractDeploymentMockUpMethodCSharpTemplate(
            ContractDeploymentCQSMessageModel contractDeploymentCQSMessageModel)
        {
            _contractDeploymentCQSMessageModel = contractDeploymentCQSMessageModel;
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
        }

        public string GenerateMethods()
        {
            var messageType = _contractDeploymentCQSMessageModel.GetTypeName();
            var messageVariableName = _contractDeploymentCQSMessageModel.GetVariableName();

            return
                $@"{SpaceUtils.Three____Tabs} /* Deployment 
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(_contractDeploymentCQSMessageModel.ConstructorABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAndWaitForReceiptAsync({messageVariableName});
{SpaceUtils.Three____Tabs}var contractAddress = transactionReceiptDeployment.ContractAddress;
{SpaceUtils.Three____Tabs} */ ";
        }
    }
}