using Nethereum.Mud.Contracts.ContractHandlers;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Mud.Contracts.World.Systems.BatchCallSystem
{
    public partial class BatchCallSystemService
    {
        public Task<TransactionReceipt> BatchCallRequestAndWaitForReceiptAsync(List<ISystemCallMulticallInput> systemCallMulticallInputs)
        {
          
            if (this.ContractHandler is MudCallFromContractHandler mudCallFromContractHandler)
            {

                return BatchCallFromRequestAndWaitForReceiptAsync(systemCallMulticallInputs.Select(x => x.GetSystemCallFromData(mudCallFromContractHandler.Delegator)).ToList());
            }
            else
            {
                return BatchCallRequestAndWaitForReceiptAsync(systemCallMulticallInputs.Select(x => x.GetSystemCallData()).ToList());
            }
        }

        public Task<string> BatchCallRequestAsync(List<ISystemCallMulticallInput> systemCallMulticallInputs)
        {

            if (this.ContractHandler is MudCallFromContractHandler mudCallFromContractHandler)
            {

                return BatchCallFromRequestAsync(systemCallMulticallInputs.Select(x => x.GetSystemCallFromData(mudCallFromContractHandler.Delegator)).ToList());
            }
            else
            {
                return BatchCallRequestAsync(systemCallMulticallInputs.Select(x => x.GetSystemCallData()).ToList());
            }
        }
    }
}
