using System;

namespace Nethereum.Unity.Rpc
{
    public interface IUnityRequest<TResult>
    {
        Exception Exception { get; set; }
        TResult Result { get; set; }
    }
}