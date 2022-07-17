using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Newtonsoft.Json;
using Nethereum.Unity.Contracts.Standards.ERC1155.ERC1155.ContractDefinition;

namespace Nethereum.Unity.Contracts.Standards.ERC1155.ERC1155
{
    public partial class Erc1155ContractRequestFactory 
    {
        public string ContractAddress { get; protected set; }
        public IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory { get; protected set; }
        public IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory { get; protected set; }
        public Erc1155ContractRequestFactory(string contractAddress, IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, IContractQueryUnityRequestFactory contractQueryUnityRequestFactory)
            {
                ContractAddress = contractAddress;
                ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
                ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
            }


        public BalanceOfQueryRequest CreateBalanceOfQueryRequest()
        {
            return new BalanceOfQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public BalanceOfBatchQueryRequest CreateBalanceOfBatchQueryRequest()
        {
            return new BalanceOfBatchQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public BurnTransactionRequest CreateBurnTransactionRequest()
        {
            return new BurnTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public BurnBatchTransactionRequest CreateBurnBatchTransactionRequest()
        {
            return new BurnBatchTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public ExistsQueryRequest CreateExistsQueryRequest()
        {
            return new ExistsQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public IsApprovedForAllQueryRequest CreateIsApprovedForAllQueryRequest()
        {
            return new IsApprovedForAllQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public MintTransactionRequest CreateMintTransactionRequest()
        {
            return new MintTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public MintBatchTransactionRequest CreateMintBatchTransactionRequest()
        {
            return new MintBatchTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public OwnerQueryRequest CreateOwnerQueryRequest()
        {
            return new OwnerQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public PauseTransactionRequest CreatePauseTransactionRequest()
        {
            return new PauseTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public PausedQueryRequest CreatePausedQueryRequest()
        {
            return new PausedQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public RenounceOwnershipTransactionRequest CreateRenounceOwnershipTransactionRequest()
        {
            return new RenounceOwnershipTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SafeBatchTransferFromTransactionRequest CreateSafeBatchTransferFromTransactionRequest()
        {
            return new SafeBatchTransferFromTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SafeTransferFromTransactionRequest CreateSafeTransferFromTransactionRequest()
        {
            return new SafeTransferFromTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SetApprovalForAllTransactionRequest CreateSetApprovalForAllTransactionRequest()
        {
            return new SetApprovalForAllTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SetTokenUriTransactionRequest CreateSetTokenUriTransactionRequest()
        {
            return new SetTokenUriTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SetURITransactionRequest CreateSetURITransactionRequest()
        {
            return new SetURITransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SupportsInterfaceQueryRequest CreateSupportsInterfaceQueryRequest()
        {
            return new SupportsInterfaceQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TotalSupplyQueryRequest CreateTotalSupplyQueryRequest()
        {
            return new TotalSupplyQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TransferOwnershipTransactionRequest CreateTransferOwnershipTransactionRequest()
        {
            return new TransferOwnershipTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public UnpauseTransactionRequest CreateUnpauseTransactionRequest()
        {
            return new UnpauseTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public UriQueryRequest CreateUriQueryRequest()
        {
            return new UriQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }

    }
}
