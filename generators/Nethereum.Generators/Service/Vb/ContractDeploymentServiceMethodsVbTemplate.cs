using System;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{
    public class ContractDeploymentServiceMethodsVbTemplate
    {
        private ContractDeploymentCQSMessageModel _contractDeploymentCQSMessageModel;
        private ServiceModel _serviceModel;

        public ContractDeploymentServiceMethodsVbTemplate(ServiceModel model)
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
                $@"{SpaceUtils.Two___Tabs}Public Shared Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Nethereum.Web3.IWeb3, ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Return web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSource)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

            var sendRequest =
                $@"{SpaceUtils.Two___Tabs} Public Shared Function DeployContractAsync(ByVal web3 As Nethereum.Web3.IWeb3, ByVal {messageVariableName} As {messageType}) As Task(Of String)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Return web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAsync({messageVariableName})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

            var sendRequestContract =
                $@"{SpaceUtils.Two___Tabs}Public Shared Async Function DeployContractAndGetServiceAsync(ByVal web3 As Nethereum.Web3.IWeb3, ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of {_serviceModel.GetTypeName()})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, cancellationTokenSource)
{SpaceUtils.Three____Tabs}Return New {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

            return String.Join(Environment.NewLine, sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}