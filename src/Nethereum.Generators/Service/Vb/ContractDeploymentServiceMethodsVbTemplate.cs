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
                $@"{SpaceUtils.TwoTabs}Public Shared Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal {messageVariableName} As {messageType}, ByVal Optional token As CancellationToken = CType(Nothing, CancellationToken)) As Task(Of TransactionReceipt)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAndWaitForReceiptAsync({messageVariableName}, token)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

            var sendRequest =
                $@"{SpaceUtils.TwoTabs} Public Shared Function DeployContractAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal {messageVariableName} As {messageType}) As Task(Of String)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return web3.Eth.GetContractDeploymentHandler(Of {messageType})().SendRequestAsync({messageVariableName})
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

            var sendRequestContract =
                $@"{SpaceUtils.TwoTabs}Public Shared Async Function DeployContractAndGetServiceAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal {messageVariableName} As {messageType}, ByVal Optional token As CancellationToken = CType(Nothing, CancellationToken)) As Task(Of {_serviceModel.GetTypeName()})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, {messageVariableName}, token)
{SpaceUtils.ThreeTabs}Return New {_serviceModel.GetTypeName()}(web3, receipt.ContractAddress)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

            return String.Join(Environment.NewLine, sendRequestReceipt, sendRequest, sendRequestContract);
        }
    }
}