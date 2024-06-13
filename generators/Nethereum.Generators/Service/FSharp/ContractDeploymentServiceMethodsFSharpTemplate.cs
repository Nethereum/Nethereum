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
                $@"{SpaceUtils.Two___Tabs}static member DeployContractAndWaitForReceiptAsync(web3: Web3, {messageVariableName}: {messageType}, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> = 
{SpaceUtils.Three____Tabs}let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
{SpaceUtils.Three____Tabs}web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSourceVal)
{SpaceUtils.Two___Tabs}";

            var sendRequest =
                $@"{SpaceUtils.Two___Tabs}static member DeployContractAsync(web3: Web3, {messageVariableName}: {messageType}): Task<string> =
{SpaceUtils.Three____Tabs}web3.Eth.GetContractDeploymentHandler<{messageType}>().SendRequestAsync({messageVariableName})
{SpaceUtils.Two___Tabs}";

            var sendRequestContract =
                $@"{SpaceUtils.Two___Tabs}static member DeployContractAndGetServiceAsync(web3: Web3, {messageVariableName}: {messageType}, ?cancellationTokenSource : CancellationTokenSource) = async {{
{SpaceUtils.Three____Tabs}let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
{SpaceUtils.Three____Tabs}let! receipt = {_serviceModel.GetTypeName()}.DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, cancellationTokenSourceVal) |> Async.AwaitTask
{SpaceUtils.Three____Tabs}return new {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress);
{SpaceUtils.Three____Tabs}}}";


            return String.Join(Environment.NewLine, sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}