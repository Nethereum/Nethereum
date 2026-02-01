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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IVerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IVerifyingPaymaster
{
    public partial class IVerifyingPaymasterService: IVerifyingPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IVerifyingPaymasterDeployment iVerifyingPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IVerifyingPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(iVerifyingPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IVerifyingPaymasterDeployment iVerifyingPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IVerifyingPaymasterDeployment>().SendRequestAsync(iVerifyingPaymasterDeployment);
        }

        public static async Task<IVerifyingPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IVerifyingPaymasterDeployment iVerifyingPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iVerifyingPaymasterDeployment, cancellationTokenSource);
            return new IVerifyingPaymasterService(web3, receipt.ContractAddress);
        }

        public IVerifyingPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IVerifyingPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public IVerifyingPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> PostOpRequestAsync(PostOpFunction postOpFunction)
        {
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(PostOpFunction postOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> PostOpRequestAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger actualUserOpFeePerGas)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ActualUserOpFeePerGas = actualUserOpFeePerGas;
            
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger actualUserOpFeePerGas, CancellationTokenSource cancellationToken = null)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ActualUserOpFeePerGas = actualUserOpFeePerGas;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> SetVerifyingSignerRequestAsync(SetVerifyingSignerFunction setVerifyingSignerFunction)
        {
             return ContractHandler.SendRequestAsync(setVerifyingSignerFunction);
        }

        public virtual Task<TransactionReceipt> SetVerifyingSignerRequestAndWaitForReceiptAsync(SetVerifyingSignerFunction setVerifyingSignerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setVerifyingSignerFunction, cancellationToken);
        }

        public virtual Task<string> SetVerifyingSignerRequestAsync(string signer)
        {
            var setVerifyingSignerFunction = new SetVerifyingSignerFunction();
                setVerifyingSignerFunction.Signer = signer;
            
             return ContractHandler.SendRequestAsync(setVerifyingSignerFunction);
        }

        public virtual Task<TransactionReceipt> SetVerifyingSignerRequestAndWaitForReceiptAsync(string signer, CancellationTokenSource cancellationToken = null)
        {
            var setVerifyingSignerFunction = new SetVerifyingSignerFunction();
                setVerifyingSignerFunction.Signer = signer;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setVerifyingSignerFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger maxCost)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.UserOpHash = userOpHash;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger maxCost, CancellationTokenSource cancellationToken = null)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.UserOpHash = userOpHash;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public Task<string> VerifyingSignerQueryAsync(VerifyingSignerFunction verifyingSignerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyingSignerFunction, string>(verifyingSignerFunction, blockParameter);
        }

        
        public virtual Task<string> VerifyingSignerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyingSignerFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(PostOpFunction),
                typeof(SetVerifyingSignerFunction),
                typeof(ValidatePaymasterUserOpFunction),
                typeof(VerifyingSignerFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(GasSponsoredEventDTO)
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
