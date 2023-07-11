using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC;
using Nethereum.RPC.Extensions.DevTools.Hardhat;

namespace Nethereum.RPC.Extensions
{

    public class HardhatService
    {
        public HardhatDropTransaction DropTransaction { get; protected set; }
        public HardhatImpersonateAccount ImpersonateAccount { get; protected set; }
        public HardhatMine Mine { get; protected set; }
        public HardhatReset Reset { get; protected set; }
        public HardhatSetBalance SetBalance { get; protected set; }
        public HardhatSetCode SetCode { get; protected set; }
        public HardhatSetCoinbase SetCoinbase { get; protected set; }
        public HardhatSetNextBlockBaseFeePerGas SetNextBlockBaseFeePerGas { get; protected set; }
        public HardhatSetNonce SetNonce { get; protected set; }
        public HardhatSetPrevRandao SetPrevRandao { get; protected set; }
        public HardhatSetStorageAt SetStorageAt { get; protected set; }
        public HardhatStopImpersonatingAccount StopImpersonatingAccount { get; protected set; }

        protected HardhatService()
        {

        }
        public HardhatService(IEthApiService ethApiService)
        {
            EthApiService = ethApiService;
            DropTransaction = new HardhatDropTransaction(ethApiService.Client);
            ImpersonateAccount = new HardhatImpersonateAccount(ethApiService.Client);
            Mine = new HardhatMine(ethApiService.Client);
            Reset = new HardhatReset(ethApiService.Client);
            SetBalance = new HardhatSetBalance(ethApiService.Client);
            SetCode = new HardhatSetCode(ethApiService.Client);
            SetCoinbase = new HardhatSetCoinbase(ethApiService.Client);
            SetNextBlockBaseFeePerGas = new HardhatSetNextBlockBaseFeePerGas(ethApiService.Client);
            SetNonce = new HardhatSetNonce(ethApiService.Client);
            SetPrevRandao = new HardhatSetPrevRandao(ethApiService.Client);
            SetStorageAt = new HardhatSetStorageAt(ethApiService.Client);
            StopImpersonatingAccount = new HardhatStopImpersonatingAccount(ethApiService.Client);
        }


        public IEthApiService EthApiService { get; protected set; }
#if !DOTNET35
        public async Task<HexBigInteger> IncreaseTimeAsync(uint numberInSeconds)
        {
            var currentBlock = await EthApiService.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            var timestamp = currentBlock.Timestamp.Value + numberInSeconds;
            await EthApiService.DevToolsEvm().SetNextBlockTimestamp.SendRequestAsync(new HexBigInteger(timestamp));
            await Mine.SendRequestAsync();
            return await EthApiService.Blocks.GetBlockNumber.SendRequestAsync();
        }
#endif

    }


}