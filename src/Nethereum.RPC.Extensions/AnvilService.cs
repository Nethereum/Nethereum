using Nethereum.RPC.Extensions.DevTools.Hardhat;

namespace Nethereum.RPC.Extensions
{
    public class AnvilService: HardhatService
    {
        public AnvilService(IEthApiService ethApiService)
        {
            EthApiService = ethApiService;
            DropTransaction = new HardhatDropTransaction(ethApiService.Client, ApiMethods.anvil_dropTransaction);
            ImpersonateAccount = new HardhatImpersonateAccount(ethApiService.Client, ApiMethods.anvil_impersonateAccount);
            Mine = new HardhatMine(ethApiService.Client, ApiMethods.anvil_mine);
            Reset = new HardhatReset(ethApiService.Client, ApiMethods.anvil_reset);
            SetBalance = new HardhatSetBalance(ethApiService.Client, ApiMethods.anvil_setBalance);
            SetCode = new HardhatSetCode(ethApiService.Client, ApiMethods.anvil_setCode);
            SetCoinbase = new HardhatSetCoinbase(ethApiService.Client, ApiMethods.anvil_setCoinbase);
            SetNextBlockBaseFeePerGas = new HardhatSetNextBlockBaseFeePerGas(ethApiService.Client, ApiMethods.anvil_setNextBlockBaseFeePerGas);
            SetNonce = new HardhatSetNonce(ethApiService.Client, ApiMethods.anvil_setNonce);
            SetPrevRandao = new HardhatSetPrevRandao(ethApiService.Client, ApiMethods.anvil_setPrevRandao);
            SetStorageAt = new HardhatSetStorageAt(ethApiService.Client, ApiMethods.anvil_setStorageAt);
            StopImpersonatingAccount = new HardhatStopImpersonatingAccount(ethApiService.Client, ApiMethods.anvil_stopImpersonatingAccount);
        }
    }


}