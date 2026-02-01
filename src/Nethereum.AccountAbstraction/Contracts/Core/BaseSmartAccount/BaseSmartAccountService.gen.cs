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
using Nethereum.AccountAbstraction.Contracts.Core.BaseSmartAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.BaseSmartAccount
{
    public partial class BaseSmartAccountService: BaseSmartAccountServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BaseSmartAccountDeployment baseSmartAccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BaseSmartAccountDeployment>().SendRequestAndWaitForReceiptAsync(baseSmartAccountDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BaseSmartAccountDeployment baseSmartAccountDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BaseSmartAccountDeployment>().SendRequestAsync(baseSmartAccountDeployment);
        }

        public static async Task<BaseSmartAccountService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BaseSmartAccountDeployment baseSmartAccountDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, baseSmartAccountDeployment, cancellationTokenSource);
            return new BaseSmartAccountService(web3, receipt.ContractAddress);
        }

        public BaseSmartAccountService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class BaseSmartAccountServiceBase: ContractWeb3ServiceBase
    {

        public BaseSmartAccountServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> UpgradeInterfaceVersionQueryAsync(UpgradeInterfaceVersionFunction upgradeInterfaceVersionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(upgradeInterfaceVersionFunction, blockParameter);
        }

        
        public virtual Task<string> UpgradeInterfaceVersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(null, blockParameter);
        }

        public virtual Task<string> AddDepositRequestAsync(AddDepositFunction addDepositFunction)
        {
             return ContractHandler.SendRequestAsync(addDepositFunction);
        }

        public virtual Task<string> AddDepositRequestAsync()
        {
             return ContractHandler.SendRequestAsync<AddDepositFunction>();
        }

        public virtual Task<TransactionReceipt> AddDepositRequestAndWaitForReceiptAsync(AddDepositFunction addDepositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addDepositFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> AddDepositRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<AddDepositFunction>(null, cancellationToken);
        }

        public Task<string> EntryPointQueryAsync(EntryPointFunction entryPointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(entryPointFunction, blockParameter);
        }

        
        public virtual Task<string> EntryPointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(null, blockParameter);
        }

        public virtual Task<string> ExecuteRequestAsync(ExecuteFunction executeFunction)
        {
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(ExecuteFunction executeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteRequestAsync(string target, BigInteger value, byte[] data)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Target = target;
                executeFunction.Value = value;
                executeFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(executeFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteRequestAndWaitForReceiptAsync(string target, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.Target = target;
                executeFunction.Value = value;
                executeFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteBatchRequestAsync(ExecuteBatchFunction executeBatchFunction)
        {
             return ContractHandler.SendRequestAsync(executeBatchFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteBatchRequestAndWaitForReceiptAsync(ExecuteBatchFunction executeBatchFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeBatchFunction, cancellationToken);
        }

        public virtual Task<string> ExecuteBatchRequestAsync(List<string> targets, List<BigInteger> values, List<byte[]> datas)
        {
            var executeBatchFunction = new ExecuteBatchFunction();
                executeBatchFunction.Targets = targets;
                executeBatchFunction.Values = values;
                executeBatchFunction.Datas = datas;
            
             return ContractHandler.SendRequestAsync(executeBatchFunction);
        }

        public virtual Task<TransactionReceipt> ExecuteBatchRequestAndWaitForReceiptAsync(List<string> targets, List<BigInteger> values, List<byte[]> datas, CancellationTokenSource cancellationToken = null)
        {
            var executeBatchFunction = new ExecuteBatchFunction();
                executeBatchFunction.Targets = targets;
                executeBatchFunction.Values = values;
                executeBatchFunction.Datas = datas;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(executeBatchFunction, cancellationToken);
        }

        public Task<BigInteger> GetDepositQueryAsync(GetDepositFunction getDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(getDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<byte[]> ProxiableUUIDQueryAsync(ProxiableUUIDFunction proxiableUUIDFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(proxiableUUIDFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ProxiableUUIDQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<string> UpgradeToAndCallRequestAsync(UpgradeToAndCallFunction upgradeToAndCallFunction)
        {
             return ContractHandler.SendRequestAsync(upgradeToAndCallFunction);
        }

        public virtual Task<TransactionReceipt> UpgradeToAndCallRequestAndWaitForReceiptAsync(UpgradeToAndCallFunction upgradeToAndCallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(upgradeToAndCallFunction, cancellationToken);
        }

        public virtual Task<string> UpgradeToAndCallRequestAsync(string newImplementation, byte[] data)
        {
            var upgradeToAndCallFunction = new UpgradeToAndCallFunction();
                upgradeToAndCallFunction.NewImplementation = newImplementation;
                upgradeToAndCallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(upgradeToAndCallFunction);
        }

        public virtual Task<TransactionReceipt> UpgradeToAndCallRequestAndWaitForReceiptAsync(string newImplementation, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var upgradeToAndCallFunction = new UpgradeToAndCallFunction();
                upgradeToAndCallFunction.NewImplementation = newImplementation;
                upgradeToAndCallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(upgradeToAndCallFunction, cancellationToken);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(ValidateUserOpFunction validateUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(ValidateUserOpFunction validateUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidateUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger missingAccountFunds)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
                validateUserOpFunction.MissingAccountFunds = missingAccountFunds;
            
             return ContractHandler.SendRequestAsync(validateUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidateUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger missingAccountFunds, CancellationTokenSource cancellationToken = null)
        {
            var validateUserOpFunction = new ValidateUserOpFunction();
                validateUserOpFunction.UserOp = userOp;
                validateUserOpFunction.UserOpHash = userOpHash;
                validateUserOpFunction.MissingAccountFunds = missingAccountFunds;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validateUserOpFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawDepositToRequestAsync(WithdrawDepositToFunction withdrawDepositToFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawDepositToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawDepositToRequestAndWaitForReceiptAsync(WithdrawDepositToFunction withdrawDepositToFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawDepositToFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawDepositToRequestAsync(string withdrawAddress, BigInteger amount)
        {
            var withdrawDepositToFunction = new WithdrawDepositToFunction();
                withdrawDepositToFunction.WithdrawAddress = withdrawAddress;
                withdrawDepositToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawDepositToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawDepositToRequestAndWaitForReceiptAsync(string withdrawAddress, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawDepositToFunction = new WithdrawDepositToFunction();
                withdrawDepositToFunction.WithdrawAddress = withdrawAddress;
                withdrawDepositToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawDepositToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(UpgradeInterfaceVersionFunction),
                typeof(AddDepositFunction),
                typeof(EntryPointFunction),
                typeof(ExecuteFunction),
                typeof(ExecuteBatchFunction),
                typeof(GetDepositFunction),
                typeof(OwnerFunction),
                typeof(ProxiableUUIDFunction),
                typeof(UpgradeToAndCallFunction),
                typeof(ValidateUserOpFunction),
                typeof(WithdrawDepositToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(InitializedEventDTO),
                typeof(UpgradedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AddressEmptyCodeError),
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(ERC1967InvalidImplementationError),
                typeof(ERC1967NonPayableError),
                typeof(ExecutionFailedError),
                typeof(FailedCallError),
                typeof(InvalidInitializationError),
                typeof(NotInitializingError),
                typeof(OnlyEntryPointError),
                typeof(OnlyOwnerError),
                typeof(OnlyOwnerOrEntryPointError),
                typeof(UUPSUnauthorizedCallContextError),
                typeof(UUPSUnsupportedProxiableUUIDError)
            };
        }
    }
}
