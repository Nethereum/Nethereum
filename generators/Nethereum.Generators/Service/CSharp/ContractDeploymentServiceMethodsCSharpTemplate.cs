using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using System;

namespace Nethereum.Generators.Service
{
    public class ContractDeploymentServiceMethodsCSharpTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        private ServiceModel _serviceModel;
        private static readonly string SpaceFollowingFunction = (Environment.NewLine + Environment.NewLine);

        public ContractDeploymentServiceMethodsCSharpTemplate(ServiceModel model)
        {
            _contractDeploymentCQSMessageModel = model.ContractDeploymentCQSMessageModel;
            _serviceModel = model;
        }

        public string GenerateMethods()
        {
            var messageType = _contractDeploymentCQSMessageModel.GetTypeName();
            var messageVariableName =
                _contractDeploymentCQSMessageModel.GetVariableName();

            var sendRequestReceipt = 
            $@"{SpaceUtils.Two___Tabs}public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, {messageType} {messageVariableName}, CancellationTokenSource cancellationTokenSource = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSource);
{SpaceUtils.Two___Tabs}}}";

            var sendRequest =
                $@"{SpaceUtils.Two___Tabs}public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, {messageType} {messageVariableName})
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAsync({messageVariableName});
{SpaceUtils.Two___Tabs}}}";

            var sendRequestContract =
                $@"{SpaceUtils.Two___Tabs}public static async Task<{_serviceModel.GetTypeName()}> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, {messageType} {messageVariableName}, CancellationTokenSource cancellationTokenSource = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var receipt = await DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, cancellationTokenSource);
{SpaceUtils.Three____Tabs}return new {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress);
{SpaceUtils.Two___Tabs}}}";

            return string.Join(SpaceFollowingFunction, sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}