using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public abstract class FunctionBase
    {
        private RpcClient rpcClient;

        private readonly Contract contract;

        public BlockParameter DefaultBlock => contract.DefaultBlock;

        public string ContractAddress => contract.Address;

        protected FunctionCallDecoder FunctionCallDecoder { get; set; }

        protected FunctionCallEncoder FunctionCallEncoder { get; set; }

        private EthCall ethCall;
        private EthSendTransaction ethSendTransaction;
        protected FunctionABI FunctionABI { get; set; }

        protected FunctionBase(RpcClient rpcClient, Contract contract, FunctionABI functionABI )
        {
            FunctionABI = functionABI;
            this.rpcClient = rpcClient;
            this.contract = contract;
            this.ethCall = new EthCall(rpcClient);
            this.ethSendTransaction = new EthSendTransaction(rpcClient);
               
            this.FunctionCallDecoder = new FunctionCallDecoder();
            this.FunctionCallEncoder = new FunctionCallEncoder();

        }
            
        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall) where TReturn: new()
        {
            var result =  await ethCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress), DefaultBlock);
            return FunctionCallDecoder.DecodeOutput<TReturn>(result, FunctionABI.OutputParameters);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, string from, HexBigInteger value) where TReturn : new()
        {
            var result = await ethCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, from, value), DefaultBlock);
            return FunctionCallDecoder.DecodeOutput<TReturn>(result, FunctionABI.OutputParameters);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, CallInput callInput) where TReturn : new()
        {
            callInput.Data = encodedFunctionCall; 
            var result = await ethCall.SendRequestAsync(callInput, DefaultBlock);
            return FunctionCallDecoder.DecodeOutput<TReturn>(result, FunctionABI.OutputParameters);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, CallInput callInput, BlockParameter block) where TReturn : new()
        {
            callInput.Data = encodedFunctionCall;
            var result = await ethCall.SendRequestAsync(callInput, block);
            return FunctionCallDecoder.DecodeOutput<TReturn>(result, FunctionABI.OutputParameters);
        }

        protected async Task<string> SendTransactionAsync(string encodedFunctionCall)
        {
            return await ethSendTransaction.SendRequestAsync(new TransactionInput(encodedFunctionCall, ContractAddress));
              
        }

        protected async Task<string> SendTransactionAsync(string encodedFunctionCall, string from, HexBigInteger value) 
        {
            return await ethSendTransaction.SendRequestAsync(new TransactionInput(encodedFunctionCall, ContractAddress, from, value));
            
        }

        protected async Task<string> SendTransactionAsync(string encodedFunctionCall,
            TransactionInput input)
        {
            input.Data = encodedFunctionCall;
            return await ethSendTransaction.SendRequestAsync(input);
        }


    }
}