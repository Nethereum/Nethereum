using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Rpc;
using System.Collections;

namespace Nethereum.Unity.Contracts
{
    public interface IContractTransactionUnityRequest : ITransactionUnityRequest
    {
        IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>() where TDeploymentMessage : ContractDeploymentMessage, new();
        IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>(TDeploymentMessage deploymentMessage) where TDeploymentMessage : ContractDeploymentMessage, new();
        IEnumerator SignAndSendTransaction<TContractFunction>(TContractFunction function, string contractAdress) where TContractFunction : FunctionMessage;
    }
}