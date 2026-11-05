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
using Nethereum.AppChain.Anchoring.ZiskVerifier.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.ZiskVerifier
{
    public partial class ZiskVerifierService: ZiskVerifierServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ZiskVerifierDeployment ziskVerifierDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ZiskVerifierDeployment>().SendRequestAndWaitForReceiptAsync(ziskVerifierDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ZiskVerifierDeployment ziskVerifierDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ZiskVerifierDeployment>().SendRequestAsync(ziskVerifierDeployment);
        }

        public static async Task<ZiskVerifierService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ZiskVerifierDeployment ziskVerifierDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, ziskVerifierDeployment, cancellationTokenSource);
            return new ZiskVerifierService(web3, receipt.ContractAddress);
        }

        public ZiskVerifierService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ZiskVerifierServiceBase: ContractWeb3ServiceBase
    {

        public ZiskVerifierServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> VersionQueryAsync(VersionFunction versionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, string>(versionFunction, blockParameter);
        }

        
        public virtual Task<string> VersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VersionFunction, string>(null, blockParameter);
        }

        public Task<byte[]> GetRootCVadcopFinalQueryAsync(GetRootCVadcopFinalFunction getRootCVadcopFinalFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRootCVadcopFinalFunction, byte[]>(getRootCVadcopFinalFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetRootCVadcopFinalQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRootCVadcopFinalFunction, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> HashPublicValuesQueryAsync(HashPublicValuesFunction hashPublicValuesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashPublicValuesFunction, BigInteger>(hashPublicValuesFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> HashPublicValuesQueryAsync(byte[] programVK, byte[] rootCVadcopFinal, byte[] publicValues, BlockParameter blockParameter = null)
        {
            var hashPublicValuesFunction = new HashPublicValuesFunction();
                hashPublicValuesFunction.ProgramVK = programVK;
                hashPublicValuesFunction.RootCVadcopFinal = rootCVadcopFinal;
                hashPublicValuesFunction.PublicValues = publicValues;
            
            return ContractHandler.QueryAsync<HashPublicValuesFunction, BigInteger>(hashPublicValuesFunction, blockParameter);
        }

        public Task<bool> VerifyProofQueryAsync(VerifyProofFunction verifyProofFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyProofFunction, bool>(verifyProofFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyProofQueryAsync(List<BigInteger> proof, List<BigInteger> pubSignals, BlockParameter blockParameter = null)
        {
            var verifyProofFunction = new VerifyProofFunction();
                verifyProofFunction.Proof = proof;
                verifyProofFunction.PubSignals = pubSignals;
            
            return ContractHandler.QueryAsync<VerifyProofFunction, bool>(verifyProofFunction, blockParameter);
        }



        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(VersionFunction),
                typeof(GetRootCVadcopFinalFunction),
                typeof(HashPublicValuesFunction),
                typeof(VerifyProofFunction),
                typeof(VerifySnarkProofFunction)
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
                typeof(InvalidProofError)
            };
        }
    }
}
