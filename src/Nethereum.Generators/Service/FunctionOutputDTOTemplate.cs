using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{

    public class ContractDeploymentServiceMethodsTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;

        private CommonGenerators _commonGenerators;
        /*
        public static Task<TransactionReceipt> DeployContractAndWaitForReceipt(Web3 web3, DeployMessage contractDeploymentMesage, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<DeployMessage>().SendRequestAndWaitForReceiptAsync(contractDeploymentMesage, cancellationTokenSource);
        }

        public static Task<string> DeployContract(Web3 web3, DeployMessage contractDeploymentMesage)
        {
            return web3.Eth.GetContractDeploymentHandler<DeployMessage>()
                .SendRequestAsync(contractDeploymentMesage);
        }

        public static async Task<Service> DeployContractAndGetService(Web3 web3, DeployMessage contractDeploymentMessage, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceipt(web3, contractDeploymentMessage, cancellationTokenSource);
            return new Service(web3, receipt.ContractAddress);
        }
        */

        public ContractDeploymentServiceMethodsTemplate()
        {
            _contractDeploymentCQSMessageModel = new ContractDeploymentCQSMessageModel();
            _commonGenerators = new CommonGenerators();
        }

        public string GenerateMethods(string contractName, ConstructorABI constructor)
        {
            var messageType = _contractDeploymentCQSMessageModel.GetContractDeploymentMessageTypeName(contractName);
            var messageVariableName =
                _contractDeploymentCQSMessageModel.GetContractDeploymentMessageVariableName(contractName);

            var sendRequestReceipt =
                $@"{SpaceUtils.TwoTabs}public Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, {messageType} {messageVariableName}, CancellationTokenSource cancellationTokenSource = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSource);
{SpaceUtils.TwoTabs}}}";


            var sendRequest =
                $@"{SpaceUtils.TwoTabs}public Task<string> DeployContractAsync(Web3 web3, {messageType} {messageVariableName})
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAsync({messageVariableName});
{SpaceUtils.TwoTabs}}}";

            var sendRequestContract =
                $@"{SpaceUtils.TwoTabs}public Task<string> DeployContractAndGetServiceAsyc(Web3 web3, {messageType} {messageVariableName}, CancellationTokenSource cancellationTokenSource = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAsync({messageVariableName});
{SpaceUtils.TwoTabs}}}";

            return null;
        }
    }



    public class FunctionServiceMethodTemplate
    {
        private FunctionOutputDTOModel _functionOutputDTOModel;
        private FunctionCQSMessageModel _functionCQSMessageModel;
        private CommonGenerators _commonGenerators;
        private ABITypeToCSharpType _abiTypeToCSharpType;

        public FunctionServiceMethodTemplate()
        {
            _functionOutputDTOModel = new FunctionOutputDTOModel();
            _commonGenerators = new CommonGenerators();
            _functionCQSMessageModel = new FunctionCQSMessageModel();
            _abiTypeToCSharpType = new ABITypeToCSharpType();
        }

        public string GenerateMethod(FunctionABI functionABI)
        {
            var messageType = _functionCQSMessageModel.GetFunctionMessageTypeName(functionABI);
            var messageVariableName = _functionCQSMessageModel.GetFunctionMessageVariableName(functionABI);

            if (_functionOutputDTOModel.CanGenerateOutputDTO(functionABI))
            {

                var functionOutputDTOType = _functionOutputDTOModel.GetFunctionOutputTypeName(functionABI);
                return
$@"{SpaceUtils.TwoTabs}public Task<{functionOutputDTOType}> {_commonGenerators.GenerateClassName(functionABI.Name)}QueryAsync({messageType} {messageVariableName}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";
            }

            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                functionABI.Constant)
            {
                var type = _abiTypeToCSharpType.GetTypeMap(functionABI.OutputParameters[0].Type);

                return
                $@"{SpaceUtils.TwoTabs}public Task<{type}> {_commonGenerators.GenerateClassName(functionABI.Name)}QueryAsync({messageType} {messageVariableName}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";
            }

            if (functionABI.Constant == false || (functionABI.OutputParameters == null || functionABI.OutputParameters.Length == 0 ))
            {
               
                var transactionRequest = 
                    $@"{SpaceUtils.TwoTabs}public Task<string> {_commonGenerators.GenerateClassName(functionABI.Name)}RequestAsync({messageType} {messageVariableName})
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAsync({messageVariableName});
{SpaceUtils.TwoTabs}}}";

                var transactionRequestAndReceipt =
                    $@"{SpaceUtils.TwoTabs}public Task<TransactionReceipt> {_commonGenerators.GenerateClassName(functionABI.Name)}RequestAndWaitForReceiptAsync({messageType} {messageVariableName}, CancellationTokenSource cancellationToken = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationToken);
{SpaceUtils.TwoTabs}}}";

                return transactionRequest + Environment.NewLine + transactionRequestAndReceipt;
            }

            return null;
        }
    }
}
 