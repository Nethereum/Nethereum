using Nethereum.RPC;
using Nethereum.RPC.Extensions.DevTools.Evm;

namespace Nethereum.RPC.Extensions
{
    public class EvmToolsService
    {
        public EvmSetNextBlockTimestamp SetNextBlockTimestamp { get; private set; }
        public EvmAddAccount AddAccount { get; private set; }
        public EvmIncreaseTime IncreaseTime { get; private set; }
        public EvmMine Mine { get; private set; }
        public EvmRemoveAccount RemoveAccount { get; private set; }
        public EvmRevert EvmRevert { get; private set; }   
        public EvmSetAccountBalance SetAccountBalance { get; private set; }
        public EvmSetAccountCode SetAccountCode { get; private set; }   
        public EvmSetAccountNonce SetAccountNonce { get; private set; } 
        public EvmSetAccountStorageAt SetAccountStorageAt { get; private set; }
        public EvmSetBlockGasLimit SetBlockGasLimit { get; private set; }
        public EvmSnapshot Snapshot { get; private set; }   

        public EvmToolsService(IEthApiService ethApiService)
        {
            EthApiService = ethApiService;
            SetNextBlockTimestamp = new EvmSetNextBlockTimestamp(EthApiService.Client);
            AddAccount = new EvmAddAccount(EthApiService.Client);
            IncreaseTime = new EvmIncreaseTime(EthApiService.Client);
            Mine = new EvmMine(EthApiService.Client);
            RemoveAccount = new EvmRemoveAccount(EthApiService.Client);
            EvmRevert = new EvmRevert(EthApiService.Client);
            SetAccountBalance = new EvmSetAccountBalance(EthApiService.Client); 
            SetAccountCode = new EvmSetAccountCode(EthApiService.Client);   
            SetAccountNonce = new EvmSetAccountNonce(EthApiService.Client);
            SetAccountStorageAt = new EvmSetAccountStorageAt(EthApiService.Client);
            SetBlockGasLimit = new EvmSetBlockGasLimit(EthApiService.Client);  
            Snapshot = new EvmSnapshot(EthApiService.Client);   
        }
        public IEthApiService EthApiService { get; }
    }


}