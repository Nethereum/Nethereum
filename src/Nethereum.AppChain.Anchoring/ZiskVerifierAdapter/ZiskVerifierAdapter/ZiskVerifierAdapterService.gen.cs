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
using Nethereum.AppChain.Anchoring.ZiskVerifierAdapter.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.ZiskVerifierAdapter
{
    public partial class ZiskVerifierAdapterService: ZiskVerifierAdapterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ZiskVerifierAdapterDeployment ziskVerifierAdapterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ZiskVerifierAdapterDeployment>().SendRequestAndWaitForReceiptAsync(ziskVerifierAdapterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ZiskVerifierAdapterDeployment ziskVerifierAdapterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ZiskVerifierAdapterDeployment>().SendRequestAsync(ziskVerifierAdapterDeployment);
        }

        public static async Task<ZiskVerifierAdapterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ZiskVerifierAdapterDeployment ziskVerifierAdapterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, ziskVerifierAdapterDeployment, cancellationTokenSource);
            return new ZiskVerifierAdapterService(web3, receipt.ContractAddress);
        }

        public ZiskVerifierAdapterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ZiskVerifierAdapterServiceBase: ContractWeb3ServiceBase
    {

        public ZiskVerifierAdapterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<ulong> ProgramVKQueryAsync(ProgramVKFunction programVKFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProgramVKFunction, ulong>(programVKFunction, blockParameter);
        }

        public virtual Task<ulong> ProgramVKQueryAsync(BigInteger index, BlockParameter blockParameter = null)
        {
            var programVKFunction = new ProgramVKFunction();
                programVKFunction.Index = index;

            return ContractHandler.QueryAsync<ProgramVKFunction, ulong>(programVKFunction, blockParameter);
        }

        public Task<ulong> RootCVadcopFinalQueryAsync(RootCVadcopFinalFunction rootCVadcopFinalFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootCVadcopFinalFunction, ulong>(rootCVadcopFinalFunction, blockParameter);
        }

        public virtual Task<ulong> RootCVadcopFinalQueryAsync(BigInteger index, BlockParameter blockParameter = null)
        {
            var rootCVadcopFinalFunction = new RootCVadcopFinalFunction();
                rootCVadcopFinalFunction.Index = index;

            return ContractHandler.QueryAsync<RootCVadcopFinalFunction, ulong>(rootCVadcopFinalFunction, blockParameter);
        }

        public Task<bool> VerifyQueryAsync(VerifyFunction verifyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyFunction, bool>(verifyFunction, blockParameter);
        }


        public virtual Task<bool> VerifyQueryAsync(byte[] proof, List<BigInteger> publicInputs, BlockParameter blockParameter = null)
        {
            var verifyFunction = new VerifyFunction();
                verifyFunction.Proof = proof;
                verifyFunction.PublicInputs = publicInputs;

            return ContractHandler.QueryAsync<VerifyFunction, bool>(verifyFunction, blockParameter);
        }

        public Task<string> ZiskVerifierQueryAsync(ZiskVerifierFunction ziskVerifierFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ZiskVerifierFunction, string>(ziskVerifierFunction, blockParameter);
        }

        public virtual Task<string> ZiskVerifierQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ZiskVerifierFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ProgramVKFunction),
                typeof(RootCVadcopFinalFunction),
                typeof(VerifyFunction),
                typeof(ZiskVerifierFunction)
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
