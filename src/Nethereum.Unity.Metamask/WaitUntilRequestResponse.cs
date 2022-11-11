using Nethereum.JsonRpc.Client.RpcMessages;
using UnityEngine;

namespace Nethereum.Unity.Metamask
{
    public class WaitUntilRequestResponse
    {
        public const int DefaultTimeOutMiliSeconds = 60000 * 60; // 1 hour
        public string Id { get; }
        public int TimeOuMiliseconds { get; }
        public bool HasTimedOut { get; private set; }
        private float _timeElapsed = 0;
        public int TimeOutMiliseconds { get; }
        public WaitUntilRequestResponse(string id, int timeOuMiliseconds = DefaultTimeOutMiliSeconds) 
        {
            Id = id;
            TimeOuMiliseconds = timeOuMiliseconds;
        }

        public bool HasCompletedResponse()
        {
            var completed = MetamaskRequestRpcClient.RequestResponses.ContainsKey(Id);
            if (completed)
            {
                HasTimedOut = false;
                return true;
            }

            _timeElapsed += (Time.deltaTime * 1000);
            if(_timeElapsed > TimeOuMiliseconds)
            {
                HasTimedOut = true;
                return true;
            }
            return false;
        }

    }
}




