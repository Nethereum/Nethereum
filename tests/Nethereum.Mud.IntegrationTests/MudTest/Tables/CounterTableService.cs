using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nethereum.Mud.IntegrationTests.MudTest.Tables.CounterTableRecord;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.TableRepository;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using System.Numerics;
using Newtonsoft.Json.Linq;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.IntegrationTests.MudTest.Tables
{
    public partial class CounterTableService : TableSingletonService<CounterTableRecord, CounterTableRecord.CounterValue>
    {
        public CounterTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> SetRecordRequestAsync(int value)
        {
            var values = new CounterValue();
            values.Value = value;

            return SetRecordRequestAsync(values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(int value)
        {
            var values = new CounterValue();
            values.Value = value;
            return SetRecordRequestAndWaitForReceiptAsync(values);
        }
    }
}
