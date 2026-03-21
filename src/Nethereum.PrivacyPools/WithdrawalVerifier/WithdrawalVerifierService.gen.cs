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
using Nethereum.PrivacyPools.WithdrawalVerifier.ContractDefinition;

namespace Nethereum.PrivacyPools.WithdrawalVerifier
{
    public partial class WithdrawalVerifierService: WithdrawalVerifierServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, WithdrawalVerifierDeployment withdrawalVerifierDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<WithdrawalVerifierDeployment>().SendRequestAndWaitForReceiptAsync(withdrawalVerifierDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, WithdrawalVerifierDeployment withdrawalVerifierDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<WithdrawalVerifierDeployment>().SendRequestAsync(withdrawalVerifierDeployment);
        }

        public static async Task<WithdrawalVerifierService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, WithdrawalVerifierDeployment withdrawalVerifierDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, withdrawalVerifierDeployment, cancellationTokenSource);
            return new WithdrawalVerifierService(web3, receipt.ContractAddress);
        }

        public WithdrawalVerifierService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class WithdrawalVerifierServiceBase: ContractWeb3ServiceBase
    {

        public WithdrawalVerifierServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<bool> VerifyProofQueryAsync(VerifyProofFunction verifyProofFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyProofFunction, bool>(verifyProofFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyProofQueryAsync(List<BigInteger> pA, List<List<BigInteger>> pB, List<BigInteger> pC, List<BigInteger> pubSignals, BlockParameter blockParameter = null)
        {
            var verifyProofFunction = new VerifyProofFunction();
                verifyProofFunction.PA = pA;
                verifyProofFunction.PB = pB;
                verifyProofFunction.PC = pC;
                verifyProofFunction.PubSignals = pubSignals;
            
            return ContractHandler.QueryAsync<VerifyProofFunction, bool>(verifyProofFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(VerifyProofFunction)
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
