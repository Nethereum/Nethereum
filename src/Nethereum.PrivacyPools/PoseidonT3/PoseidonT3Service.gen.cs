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
using Nethereum.PrivacyPools.PoseidonT3.ContractDefinition;

namespace Nethereum.PrivacyPools.PoseidonT3
{
    public partial class PoseidonT3Service: PoseidonT3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, PoseidonT3Deployment poseidonT3Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PoseidonT3Deployment>().SendRequestAndWaitForReceiptAsync(poseidonT3Deployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, PoseidonT3Deployment poseidonT3Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PoseidonT3Deployment>().SendRequestAsync(poseidonT3Deployment);
        }

        public static async Task<PoseidonT3Service> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, PoseidonT3Deployment poseidonT3Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, poseidonT3Deployment, cancellationTokenSource);
            return new PoseidonT3Service(web3, receipt.ContractAddress);
        }

        public PoseidonT3Service(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class PoseidonT3ServiceBase: ContractWeb3ServiceBase
    {

        public PoseidonT3ServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> HashQueryAsync(HashFunction hashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashFunction, BigInteger>(hashFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> HashQueryAsync(List<BigInteger> returnValue1, BlockParameter blockParameter = null)
        {
            var hashFunction = new HashFunction();
                hashFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<HashFunction, BigInteger>(hashFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(HashFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
