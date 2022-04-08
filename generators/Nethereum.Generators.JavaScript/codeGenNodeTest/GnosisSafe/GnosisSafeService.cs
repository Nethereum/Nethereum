using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.GnosisSafe.GnosisSafe.ContractDefinition;

namespace Nethereum.GnosisSafe.GnosisSafe
{
    public partial class GnosisSafeService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, GnosisSafeDeployment gnosisSafeDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<GnosisSafeDeployment>().SendRequestAndWaitForReceiptAsync(gnosisSafeDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, GnosisSafeDeployment gnosisSafeDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<GnosisSafeDeployment>().SendRequestAsync(gnosisSafeDeployment);
        }

        public static async Task<GnosisSafeService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, GnosisSafeDeployment gnosisSafeDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, gnosisSafeDeployment, cancellationTokenSource);
            return new GnosisSafeService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public GnosisSafeService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<GetTestOutputDTO> GetTestQueryAsync(GetTestFunction getTestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTestFunction, GetTestOutputDTO>(getTestFunction, blockParameter);
        }

        public Task<GetTestOutputDTO> GetTestQueryAsync(ERC20OwnerInfo ownerInfo, BlockParameter blockParameter = null)
        {
            var getTestFunction = new GetTestFunction();
                getTestFunction.OwnerInfo = ownerInfo;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetTestFunction, GetTestOutputDTO>(getTestFunction, blockParameter);
        }

        public Task<GetTest2OutputDTO> GetTest2QueryAsync(GetTest2Function getTest2Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTest2Function, GetTest2OutputDTO>(getTest2Function, blockParameter);
        }

        public Task<GetTest2OutputDTO> GetTest2QueryAsync(InsideOwnerInfo ownerInfo, BlockParameter blockParameter = null)
        {
            var getTest2Function = new GetTest2Function();
                getTest2Function.OwnerInfo = ownerInfo;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetTest2Function, GetTest2OutputDTO>(getTest2Function, blockParameter);
        }
    }
}
