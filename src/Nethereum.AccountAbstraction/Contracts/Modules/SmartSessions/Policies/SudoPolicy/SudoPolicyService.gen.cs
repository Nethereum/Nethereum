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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy
{
    public partial class SudoPolicyService: SudoPolicyServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SudoPolicyDeployment sudoPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SudoPolicyDeployment>().SendRequestAndWaitForReceiptAsync(sudoPolicyDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SudoPolicyDeployment sudoPolicyDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SudoPolicyDeployment>().SendRequestAsync(sudoPolicyDeployment);
        }

        public static async Task<SudoPolicyService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SudoPolicyDeployment sudoPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, sudoPolicyDeployment, cancellationTokenSource);
            return new SudoPolicyService(web3, receipt.ContractAddress);
        }

        public SudoPolicyService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SudoPolicyServiceBase: ContractWeb3ServiceBase
    {

        public SudoPolicyServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<bool> Check1271SignedActionQueryAsync(Check1271SignedActionFunction check1271SignedActionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Check1271SignedActionFunction, bool>(check1271SignedActionFunction, blockParameter);
        }

        
        public virtual Task<bool> Check1271SignedActionQueryAsync(byte[] id, string requestSender, string account, byte[] hash, byte[] signature, BlockParameter blockParameter = null)
        {
            var check1271SignedActionFunction = new Check1271SignedActionFunction();
                check1271SignedActionFunction.Id = id;
                check1271SignedActionFunction.RequestSender = requestSender;
                check1271SignedActionFunction.Account = account;
                check1271SignedActionFunction.Hash = hash;
                check1271SignedActionFunction.Signature = signature;
            
            return ContractHandler.QueryAsync<Check1271SignedActionFunction, bool>(check1271SignedActionFunction, blockParameter);
        }

        public Task<BigInteger> CheckActionQueryAsync(CheckActionFunction checkActionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CheckActionFunction, BigInteger>(checkActionFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CheckActionQueryAsync(byte[] returnValue1, string returnValue2, string returnValue3, BigInteger returnValue4, byte[] returnValue5, BlockParameter blockParameter = null)
        {
            var checkActionFunction = new CheckActionFunction();
                checkActionFunction.ReturnValue1 = returnValue1;
                checkActionFunction.ReturnValue2 = returnValue2;
                checkActionFunction.ReturnValue3 = returnValue3;
                checkActionFunction.ReturnValue4 = returnValue4;
                checkActionFunction.ReturnValue5 = returnValue5;
            
            return ContractHandler.QueryAsync<CheckActionFunction, BigInteger>(checkActionFunction, blockParameter);
        }

        public Task<BigInteger> CheckUserOpPolicyQueryAsync(CheckUserOpPolicyFunction checkUserOpPolicyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CheckUserOpPolicyFunction, BigInteger>(checkUserOpPolicyFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CheckUserOpPolicyQueryAsync(byte[] returnValue1, PackedUserOperation returnValue2, BlockParameter blockParameter = null)
        {
            var checkUserOpPolicyFunction = new CheckUserOpPolicyFunction();
                checkUserOpPolicyFunction.ReturnValue1 = returnValue1;
                checkUserOpPolicyFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<CheckUserOpPolicyFunction, BigInteger>(checkUserOpPolicyFunction, blockParameter);
        }

        public virtual Task<string> InitializeWithMultiplexerRequestAsync(InitializeWithMultiplexerFunction initializeWithMultiplexerFunction)
        {
             return ContractHandler.SendRequestAsync(initializeWithMultiplexerFunction);
        }

        public virtual Task<TransactionReceipt> InitializeWithMultiplexerRequestAndWaitForReceiptAsync(InitializeWithMultiplexerFunction initializeWithMultiplexerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeWithMultiplexerFunction, cancellationToken);
        }

        public virtual Task<string> InitializeWithMultiplexerRequestAsync(string account, byte[] configId, byte[] returnValue3)
        {
            var initializeWithMultiplexerFunction = new InitializeWithMultiplexerFunction();
                initializeWithMultiplexerFunction.Account = account;
                initializeWithMultiplexerFunction.ConfigId = configId;
                initializeWithMultiplexerFunction.ReturnValue3 = returnValue3;
            
             return ContractHandler.SendRequestAsync(initializeWithMultiplexerFunction);
        }

        public virtual Task<TransactionReceipt> InitializeWithMultiplexerRequestAndWaitForReceiptAsync(string account, byte[] configId, byte[] returnValue3, CancellationTokenSource cancellationToken = null)
        {
            var initializeWithMultiplexerFunction = new InitializeWithMultiplexerFunction();
                initializeWithMultiplexerFunction.Account = account;
                initializeWithMultiplexerFunction.ConfigId = configId;
                initializeWithMultiplexerFunction.ReturnValue3 = returnValue3;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeWithMultiplexerFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceID = interfaceID;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(Check1271SignedActionFunction),
                typeof(CheckActionFunction),
                typeof(CheckUserOpPolicyFunction),
                typeof(InitializeWithMultiplexerFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(PolicySetEventDTO),
                typeof(SudoPolicyInstalledMultiplexerEventDTO),
                typeof(SudoPolicyRemovedEventDTO),
                typeof(SudoPolicySetEventDTO),
                typeof(SudoPolicyUninstalledAllAccountEventDTO)
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
