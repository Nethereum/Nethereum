using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Nethereum.Unity.Util
{
#endif

#if !DOTNET35

    public static class WaitUntilExtension
    {

        public static IEnumerator WaitUntil<T>(this Task<T> task, Action<T> response, Action<Exception> error)
        {

            yield return new WaitUntil(() => task.IsCompleted);


            if (!task.IsFaulted)
            {
                response(task.Result);
            }

            else
            {
                if (task.Exception != null)
                {
                    error(task.Exception.InnerException ?? task.Exception);
                }
                else
                {
                    error(new Exception("Wait Until Task failed:" + task.ToString()));
                }
               
            }
        }


        public static IEnumerator WaitUntilInBackgroundThread<T>(this Task<T> task, Action<T> response, Action<Exception> error)
        {

            var runTask = Task.Run(() => task);


            yield return new WaitUntil(() => runTask.IsCompleted);

            if (runTask.Exception != null)
            {
                error(runTask.Exception.InnerException ?? runTask.Exception);
            }
            else
            {
                error(new Exception("Wait Until Task failed:" + task.ToString()));
            }
        }
    }
#endif
}
