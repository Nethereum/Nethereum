using System;
using System.Collections.Generic;

namespace Nethereum.Web3.Contracts.Comparers
{
    public class EventLogBlockNumberTransactionIndexComparer : IComparer<object>
    {
        public int Compare(object x, object y)
        {
            var xLog = x as IEventLog;
            var yLog = y as IEventLog;
            if ((xLog == null) || (yLog == null)) throw new Exception("Both instances should implement IEventLog");
            return new FilterLogBlockNumberTransactionIndexComparer().Compare(xLog.Log, yLog.Log);
        }
    }
}