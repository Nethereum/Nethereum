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
using Nethereum.Optimism.Lib_AddressManager.ContractDefinition;

namespace Nethereum.Optimism.Lib_AddressManager
{
    public partial class Lib_AddressManagerService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, Lib_AddressManagerDeployment lib_AddressManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<Lib_AddressManagerDeployment>().SendRequestAndWaitForReceiptAsync(lib_AddressManagerDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, Lib_AddressManagerDeployment lib_AddressManagerDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<Lib_AddressManagerDeployment>().SendRequestAsync(lib_AddressManagerDeployment);
        }

        public static async Task<Lib_AddressManagerService> DeployContractAndGetServiceAsync(Web3.Web3 web3, Lib_AddressManagerDeployment lib_AddressManagerDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, lib_AddressManagerDeployment, cancellationTokenSource);
            return new Lib_AddressManagerService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public Lib_AddressManagerService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> GetAddressQueryAsync(GetAddressFunction getAddressFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }


        public Task<string> GetAddressQueryAsync(string name, BlockParameter blockParameter = null)
        {
            var getAddressFunction = new GetAddressFunction();
            getAddressFunction.Name = name;

            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }


        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public Task<string> RenounceOwnershipRequestAsync()
        {
            return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<string> SetAddressRequestAsync(SetAddressFunction setAddressFunction)
        {
            return ContractHandler.SendRequestAsync(setAddressFunction);
        }

        public Task<TransactionReceipt> SetAddressRequestAndWaitForReceiptAsync(SetAddressFunction setAddressFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setAddressFunction, cancellationToken);
        }

        public Task<string> SetAddressRequestAsync(string name, string address)
        {
            var setAddressFunction = new SetAddressFunction();
            setAddressFunction.Name = name;
            setAddressFunction.Address = address;

            return ContractHandler.SendRequestAsync(setAddressFunction);
        }

        public Task<TransactionReceipt> SetAddressRequestAndWaitForReceiptAsync(string name, string address, CancellationTokenSource cancellationToken = null)
        {
            var setAddressFunction = new SetAddressFunction();
            setAddressFunction.Name = name;
            setAddressFunction.Address = address;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setAddressFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }
    }
}
