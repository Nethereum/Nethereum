using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nethereum.Mud.IntegrationTests.MudTest.Tables.CounterTableRecord;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Mud.IntegrationTests.MudTest.Tables
{
    public class CounterTableRecord : TableRecordSingleton<CounterValue>
    {
        public CounterTableRecord() : base("Counter")
        {
        }
        public class CounterValue
        {
            [Parameter("uint32", "value", 1)]
            public int Value { get; set; }
        }
    }
}
