using Nethereum.JsonRpc.Client.RpcMessages;
using System.Collections.Generic;
using UnityEngine;

namespace Nethereum.Unity.Rpc
{
    public class WaitUntilRequestResponse
    {
        public const int DefaultTimeOutMilliSeconds = 60000 * 60; // 1 hour
        public string Id { get; }
        public IDictionary<string, RpcResponseMessage> RequestResponses { get; }
        public int TimeOutMilliseconds { get; }
        public bool HasTimedOut { get; private set; }
        private float _timeElapsed = 0;
        public WaitUntilRequestResponse(string id, IDictionary<string, RpcResponseMessage> requestResponses, int timeOutMilliseconds = DefaultTimeOutMilliSeconds) 
        {
            Id = id;
            RequestResponses = requestResponses;
            TimeOutMilliseconds = timeOutMilliseconds;
        }

        public bool HasCompletedResponse()
        {
            var completed = RequestResponses.ContainsKey(Id);
            if (completed)
            {
                HasTimedOut = false;
                return true;
            }

            _timeElapsed += (Time.deltaTime * 1000);
            if(_timeElapsed > TimeOutMilliseconds)
            {
                HasTimedOut = true;
                return true;
            }
            return false;
        }

    }
}




