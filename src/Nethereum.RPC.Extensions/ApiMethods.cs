using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions
{
    public enum ApiMethods
    {
        //evm_test
        evm_addAccount,
        evm_increaseTime,
        evm_mine,
        evm_removeAccount,
        evm_revert,
        evm_setAccountBalance,
        evm_setAccountCode,
        evm_setAccountNonce,
        evm_setAccountStorageAt,
        evm_setTime,
        evm_snapshot,
        evm_setNextBlockTimestamp,
        evm_setBlockGasLimit,

        //hardhat
        hardhat_impersonateAccount,
        hardhat_mine,
        hardhat_reset,
        hardhat_setBalance,
        hardhat_setCode,
        hardhat_setStorageAt,
        hardhat_setNonce,
        hardhat_setPrevRandao,
        hardhat_setNextBlockBaseFeePerGas,
        hardhat_dropTransaction,
        hardhat_stopImpersonatingAccount,
        hardhat_setCoinbase,

        //anvil
        anvil_impersonateAccount,
        anvil_mine,
        anvil_reset,
        anvil_setBalance,
        anvil_setCode,
        anvil_setStorageAt,
        anvil_setNonce,
        anvil_setPrevRandao,
        anvil_setNextBlockBaseFeePerGas,
        anvil_dropTransaction,
        anvil_stopImpersonatingAccount,
        anvil_setCoinbase
    }
}