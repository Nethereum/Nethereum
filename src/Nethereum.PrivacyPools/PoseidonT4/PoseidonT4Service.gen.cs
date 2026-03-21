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
using Nethereum.PrivacyPools.PoseidonT4.ContractDefinition;

namespace Nethereum.PrivacyPools.PoseidonT4
{
    public partial class PoseidonT4Service: PoseidonT4ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, PoseidonT4Deployment poseidonT4Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PoseidonT4Deployment>().SendRequestAndWaitForReceiptAsync(poseidonT4Deployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, PoseidonT4Deployment poseidonT4Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PoseidonT4Deployment>().SendRequestAsync(poseidonT4Deployment);
        }

        public static async Task<PoseidonT4Service> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, PoseidonT4Deployment poseidonT4Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, poseidonT4Deployment, cancellationTokenSource);
            return new PoseidonT4Service(web3, receipt.ContractAddress);
        }

        public PoseidonT4Service(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class PoseidonT4ServiceBase: ContractWeb3ServiceBase
    {

        public PoseidonT4ServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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
