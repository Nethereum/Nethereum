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
using Nethereum.Mud.Contracts.BalanceTransferSystem.ContractDefinition;

namespace Nethereum.Mud.Contracts.BalanceTransferSystem
{
    public partial class BalanceTransferSystemService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BalanceTransferSystemDeployment balanceTransferSystemDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BalanceTransferSystemDeployment>().SendRequestAndWaitForReceiptAsync(balanceTransferSystemDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BalanceTransferSystemDeployment balanceTransferSystemDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BalanceTransferSystemDeployment>().SendRequestAsync(balanceTransferSystemDeployment);
        }

        public static async Task<BalanceTransferSystemService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BalanceTransferSystemDeployment balanceTransferSystemDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, balanceTransferSystemDeployment, cancellationTokenSource);
            return new BalanceTransferSystemService(web3, receipt.ContractAddress);
        }

        public BalanceTransferSystemService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> MsgSenderQueryAsync(MsgSenderFunction msgSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(msgSenderFunction, blockParameter);
        }

        
        public Task<string> MsgSenderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> MsgValueQueryAsync(MsgValueFunction msgValueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgValueFunction, BigInteger>(msgValueFunction, blockParameter);
        }

        
        public Task<BigInteger> MsgValueQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgValueFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> WorldQueryAsync(WorldFunction worldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldFunction, string>(worldFunction, blockParameter);
        }

        
        public Task<string> WorldQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldFunction, string>(null, blockParameter);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<string> TransferBalanceToAddressRequestAsync(TransferBalanceToAddressFunction transferBalanceToAddressFunction)
        {
             return ContractHandler.SendRequestAsync(transferBalanceToAddressFunction);
        }

        public Task<TransactionReceipt> TransferBalanceToAddressRequestAndWaitForReceiptAsync(TransferBalanceToAddressFunction transferBalanceToAddressFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferBalanceToAddressFunction, cancellationToken);
        }

        public Task<string> TransferBalanceToAddressRequestAsync(byte[] fromNamespaceId, string toAddress, BigInteger amount)
        {
            var transferBalanceToAddressFunction = new TransferBalanceToAddressFunction();
                transferBalanceToAddressFunction.FromNamespaceId = fromNamespaceId;
                transferBalanceToAddressFunction.ToAddress = toAddress;
                transferBalanceToAddressFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(transferBalanceToAddressFunction);
        }

        public Task<TransactionReceipt> TransferBalanceToAddressRequestAndWaitForReceiptAsync(byte[] fromNamespaceId, string toAddress, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var transferBalanceToAddressFunction = new TransferBalanceToAddressFunction();
                transferBalanceToAddressFunction.FromNamespaceId = fromNamespaceId;
                transferBalanceToAddressFunction.ToAddress = toAddress;
                transferBalanceToAddressFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferBalanceToAddressFunction, cancellationToken);
        }

        public Task<string> TransferBalanceToNamespaceRequestAsync(TransferBalanceToNamespaceFunction transferBalanceToNamespaceFunction)
        {
             return ContractHandler.SendRequestAsync(transferBalanceToNamespaceFunction);
        }

        public Task<TransactionReceipt> TransferBalanceToNamespaceRequestAndWaitForReceiptAsync(TransferBalanceToNamespaceFunction transferBalanceToNamespaceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferBalanceToNamespaceFunction, cancellationToken);
        }

        public Task<string> TransferBalanceToNamespaceRequestAsync(byte[] fromNamespaceId, byte[] toNamespaceId, BigInteger amount)
        {
            var transferBalanceToNamespaceFunction = new TransferBalanceToNamespaceFunction();
                transferBalanceToNamespaceFunction.FromNamespaceId = fromNamespaceId;
                transferBalanceToNamespaceFunction.ToNamespaceId = toNamespaceId;
                transferBalanceToNamespaceFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(transferBalanceToNamespaceFunction);
        }

        public Task<TransactionReceipt> TransferBalanceToNamespaceRequestAndWaitForReceiptAsync(byte[] fromNamespaceId, byte[] toNamespaceId, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var transferBalanceToNamespaceFunction = new TransferBalanceToNamespaceFunction();
                transferBalanceToNamespaceFunction.FromNamespaceId = fromNamespaceId;
                transferBalanceToNamespaceFunction.ToNamespaceId = toNamespaceId;
                transferBalanceToNamespaceFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferBalanceToNamespaceFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MsgSenderFunction),
                typeof(MsgValueFunction),
                typeof(WorldFunction),
                typeof(SupportsInterfaceFunction),
                typeof(TransferBalanceToAddressFunction),
                typeof(TransferBalanceToNamespaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(StoreSplicestaticdataEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(SliceOutofboundsError),
                typeof(UnauthorizedCallContextError),
                typeof(WorldAccessdeniedError),
                typeof(WorldAlreadyinitializedError),
                typeof(WorldCallbacknotallowedError),
                typeof(WorldDelegationnotfoundError),
                typeof(WorldFunctionselectoralreadyexistsError),
                typeof(WorldFunctionselectornotfoundError),
                typeof(WorldInsufficientbalanceError),
                typeof(WorldInterfacenotsupportedError),
                typeof(WorldInvalidnamespaceError),
                typeof(WorldInvalidresourceidError),
                typeof(WorldInvalidresourcetypeError),
                typeof(WorldResourcealreadyexistsError),
                typeof(WorldResourcenotfoundError),
                typeof(WorldSystemalreadyexistsError),
                typeof(WorldUnlimiteddelegationnotallowedError)
            };
        }
    }
}
