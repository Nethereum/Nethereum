using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ContractDeploymentServiceMethodsFSharpTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        private ServiceModel _serviceModel;

        public ContractDeploymentServiceMethodsFSharpTemplate(ServiceModel model)
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
                $@"{SpaceUtils.TwoTabs}static member DeployContractAndWaitForReceiptAsync(web3: Web3, {messageVariableName}: {messageType}, ?token : CancellationToken): Task<TransactionReceipt> = 
{SpaceUtils.ThreeTabs}let cancellationTokenSourceVal = defaultArg token null
{SpaceUtils.ThreeTabs}web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSourceVal)
{SpaceUtils.TwoTabs}";

            var sendRequest =
                $@"{SpaceUtils.TwoTabs}static member DeployContractAsync(web3: Web3, {messageVariableName}: {messageType}): Task<string> =
{SpaceUtils.ThreeTabs}web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAsync({messageVariableName})
{SpaceUtils.TwoTabs}";

            var sendRequestContract =
                $@"{SpaceUtils.TwoTabs}static member DeployContractAndGetServiceAsync(web3: Web3, {messageVariableName}: {messageType}, ?token : CancellationToken) = async {{
{SpaceUtils.ThreeTabs}let cancellationTokenSourceVal = defaultArg token null
{SpaceUtils.ThreeTabs}let! receipt = {_serviceModel.GetTypeName()}.DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, cancellationTokenSourceVal) |> Async.AwaitTask
{SpaceUtils.ThreeTabs}return new {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress);
{SpaceUtils.ThreeTabs}}}";


            return String.Join(Environment.NewLine, sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}