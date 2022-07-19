using System;

namespace Nethereum.Unity.Rpc
{


    public class UnityRequest<TResult> : IUnityRequest<TResult>
    {
        public TResult Result { get; set; }
        public Exception Exception { get; set; }
    }
}
