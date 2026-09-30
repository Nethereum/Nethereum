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
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry.ContractDefinition;

namespace Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry
{
    public partial class BookingRegistryService: BookingRegistryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BookingRegistryDeployment bookingRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BookingRegistryDeployment>().SendRequestAndWaitForReceiptAsync(bookingRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BookingRegistryDeployment bookingRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BookingRegistryDeployment>().SendRequestAsync(bookingRegistryDeployment);
        }

        public static async Task<BookingRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BookingRegistryDeployment bookingRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, bookingRegistryDeployment, cancellationTokenSource);
            return new BookingRegistryService(web3, receipt.ContractAddress);
        }

        public BookingRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class BookingRegistryServiceBase: ContractWeb3ServiceBase
    {

        public BookingRegistryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> BookRequestAsync(BookFunction bookFunction)
        {
             return ContractHandler.SendRequestAsync(bookFunction);
        }

        public virtual Task<TransactionReceipt> BookRequestAndWaitForReceiptAsync(BookFunction bookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(bookFunction, cancellationToken);
        }

        public virtual Task<string> BookRequestAsync(BigInteger slot)
        {
            var bookFunction = new BookFunction();
                bookFunction.Slot = slot;
            
             return ContractHandler.SendRequestAsync(bookFunction);
        }

        public virtual Task<TransactionReceipt> BookRequestAndWaitForReceiptAsync(BigInteger slot, CancellationTokenSource cancellationToken = null)
        {
            var bookFunction = new BookFunction();
                bookFunction.Slot = slot;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(bookFunction, cancellationToken);
        }

        public Task<string> GuestOfQueryAsync(GuestOfFunction guestOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GuestOfFunction, string>(guestOfFunction, blockParameter);
        }

        
        public virtual Task<string> GuestOfQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var guestOfFunction = new GuestOfFunction();
                guestOfFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<GuestOfFunction, string>(guestOfFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> ReleaseRequestAsync(ReleaseFunction releaseFunction)
        {
             return ContractHandler.SendRequestAsync(releaseFunction);
        }

        public virtual Task<TransactionReceipt> ReleaseRequestAndWaitForReceiptAsync(ReleaseFunction releaseFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(releaseFunction, cancellationToken);
        }

        public virtual Task<string> ReleaseRequestAsync(BigInteger slot)
        {
            var releaseFunction = new ReleaseFunction();
                releaseFunction.Slot = slot;
            
             return ContractHandler.SendRequestAsync(releaseFunction);
        }

        public virtual Task<TransactionReceipt> ReleaseRequestAndWaitForReceiptAsync(BigInteger slot, CancellationTokenSource cancellationToken = null)
        {
            var releaseFunction = new ReleaseFunction();
                releaseFunction.Slot = slot;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(releaseFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(BookFunction),
                typeof(GuestOfFunction),
                typeof(OwnerFunction),
                typeof(ReleaseFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(SlotBookedEventDTO),
                typeof(SlotReleasedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(SlotAlreadyBookedError)
            };
        }
    }
}
