using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount
{
    public partial class NethereumAccountService
    {
        public async Task<bool> IsDeployedAsync()
        {
            var code = await Web3.Eth.GetCode.SendRequestAsync(ContractAddress);
            return !string.IsNullOrEmpty(code) && code != "0x";
        }

        public Task<TransactionReceipt> ExecuteAsync(Call call)
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeSingle(call.Target, call.Value, call.Data);
            return ExecuteRequestAndWaitForReceiptAsync(mode, executionCalldata);
        }

        public Task<TransactionReceipt> ExecuteBatchAsync(Call[] calls)
        {
            var mode = ERC7579ModeLib.EncodeBatchDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeBatch(calls);
            return ExecuteRequestAndWaitForReceiptAsync(mode, executionCalldata);
        }

        public Task<TransactionReceipt> AddDepositAsync(BigInteger amount)
        {
            var function = new AddDepositFunction { AmountToSend = amount };
            return ContractHandler.SendRequestAndWaitForReceiptAsync(function);
        }
    }
}
