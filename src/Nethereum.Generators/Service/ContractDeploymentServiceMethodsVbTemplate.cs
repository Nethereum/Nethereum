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
                $@"{SpaceUtils.TwoTabs}Public Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Web3, ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSource)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

            var sendRequest =
                $@"{SpaceUtils.TwoTabs} Public Function DeployContractAsync(ByVal web3 As Web3, ByVal {messageVariableName} As {messageType}) As Task(Of String)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAsync({messageVariableName})
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

            var sendRequestContract =
                $@"{SpaceUtils.TwoTabs}Public Async Function DeployContractAndGetServiceAsync(ByVal web3 As Web3, ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of {_serviceModel.GetTypeName()})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, cancellationTokenSource)
{SpaceUtils.ThreeTabs}Return New {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

            return String.Join(Environment.NewLine, sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}