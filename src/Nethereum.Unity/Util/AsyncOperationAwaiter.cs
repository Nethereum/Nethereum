using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nethereum.Unity.Util
{

#if !DOTNET35
    public struct AsyncOperationAwaiter : INotifyCompletion
    {
        private AsyncOperation _asyncOperation;

        public bool IsCompleted => _asyncOperation.isDone;

        public AsyncOperationAwaiter(AsyncOperation asyncOperation) => _asyncOperation = asyncOperation;

        public void GetResult() { }

        public void OnCompleted(Action continuation)
        {
            _asyncOperation.completed += _ => continuation();
        }
    }

    public static class AsyncOperationAwaitable
    {
        public static AsyncOperationAwaiter GetAwaiter(this AsyncOperation operation)
        {
            return new AsyncOperationAwaiter(operation);
        }
    }
#endif
}
