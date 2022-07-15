using System;

namespace Nethereum.JsonRpc.UnityClient
{
    public interface IUnityRequest<TResult>
    {
        Exception Exception { get; set; }
        TResult Result { get; set; }
    }
}