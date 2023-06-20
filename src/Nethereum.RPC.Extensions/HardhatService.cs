using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC;
using Nethereum.RPC.Extensions.DevTools.Hardhat;

namespace Nethereum.RPC.Extensions
{
    public class HardhatService
    {
        public HardhatDropTransaction DropTransaction { get; private set; }
        public HardhatImpersonateAccount ImpersonateAccount { get; private set; }
        public HardhatMine Mine { get; private set; }
        public HardhatReset Reset { get; private set; }
        public HardhatSetBalance SetBalance { get; private set; }
        public HardhatSetCode SetCode { get; private set; }
        public HardhatSetCoinbase SetCoinbase { get; private set; }
        public HardhatSetNextBlockBaseFeePerGas SetNextBlockBaseFeePerGas { get; private set; }
        public HardhatSetNonce SetNonce { get; private set; }
        public HardhatSetPrevRandao SetPrevRandao { get; private set; }
        public HardhatSetStorageAt SetStorageAt { get; private set; }
        public HardhatStopImpersonatingAccount StopImpersonatingAccount { get; private set; }

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


        public IEthApiService EthApiService { get; }
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