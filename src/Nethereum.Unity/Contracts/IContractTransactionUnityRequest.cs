using Nethereum.Contracts;
using Nethereum.JsonRpc.UnityClient;
using System.Collections;

namespace Nethereum.Unity.Contracts
{
    public interface IContractTransactionUnityRequest : IUnityRequest<string>
    {
        IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>() where TDeploymentMessage : ContractDeploymentMessage, new();
        IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>(TDeploymentMessage deploymentMessage) where TDeploymentMessage : ContractDeploymentMessage;
        IEnumerator SignAndSendTransaction<TContractFunction>(TContractFunction function, string contractAdress) where TContractFunction : FunctionMessage;
    }
}