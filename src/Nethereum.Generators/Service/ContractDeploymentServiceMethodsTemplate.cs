using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class ContractDeploymentServiceMethodsTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        private ServiceModel _serviceModel;

        public ContractDeploymentServiceMethodsTemplate(ServiceModel model)
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
                $@"{SpaceUtils.TwoTabs}public Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, {messageType} {messageVariableName}, CancellationTokenSource cancellationTokenSource = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSource);
{SpaceUtils.TwoTabs}}}";

            var sendRequest =
                $@"{SpaceUtils.TwoTabs}public Task<string> DeployContractAsync(Web3 web3, {messageType} {messageVariableName})
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAsync({messageVariableName});
{SpaceUtils.TwoTabs}}}";

            var sendRequestContract =
                $@"{SpaceUtils.TwoTabs}public Task<{_serviceModel.GetTypeName()}> DeployContractAndGetServiceAsync(Web3 web3, {messageType} {messageVariableName}, CancellationTokenSource cancellationTokenSource = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var receipt = DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, cancellationTokenSource);
{SpaceUtils.ThreeTabs}return new {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress);
{SpaceUtils.TwoTabs}}}";

            return String.Join(Environment.NewLine,sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}