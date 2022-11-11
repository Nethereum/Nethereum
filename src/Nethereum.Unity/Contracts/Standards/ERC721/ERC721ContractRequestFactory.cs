using System.Collections.Generic;
using System;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;

namespace Nethereum.Unity.Contracts.Standards.ERC721
{

    public partial class ERC721ContractRequestFactory 
    {
        public string ContractAddress { get; protected set; }
        public IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory { get; protected set; }
        public IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory { get; protected set; }
        public ERC721ContractRequestFactory(string contractAddress, IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, IContractQueryUnityRequestFactory contractQueryUnityRequestFactory)
            {
                ContractAddress = contractAddress;
                ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
                ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
            }

        public ApproveTransactionRequest CreateApproveTransactionRequest()
        {
            return new ApproveTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }

        public BalanceOfQueryRequest CreateBalanceOfQueryRequest()
        {
            return new BalanceOfQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public GetApprovedQueryRequest CreateGetApprovedQueryRequest()
        {
            return new GetApprovedQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public IsApprovedForAllQueryRequest CreateIsApprovedForAllQueryRequest()
        {
            return new IsApprovedForAllQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }

        public NameQueryRequest CreateNameQueryRequest()
        {
            return new NameQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }

        public OwnerOfQueryRequest CreateOwnerOfQueryRequest()
        {
            return new OwnerOfQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }

        public SafeTransferFromTransactionRequest CreateSafeTransferFromTransactionRequest()
        {
            return new SafeTransferFromTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }

        public SafeTransferFrom1TransactionRequest CreateSafeTransferFrom1TransactionRequest()
        {
            return new SafeTransferFrom1TransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SetApprovalForAllTransactionRequest CreateSetApprovalForAllTransactionRequest()
        {
            return new SetApprovalForAllTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public SupportsInterfaceQueryRequest CreateSupportsInterfaceQueryRequest()
        {
            return new SupportsInterfaceQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public SymbolQueryRequest CreateSymbolQueryRequest()
        {
            return new SymbolQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TokenByIndexQueryRequest CreateTokenByIndexQueryRequest()
        {
            return new TokenByIndexQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TokenOfOwnerByIndexQueryRequest CreateTokenOfOwnerByIndexQueryRequest()
        {
            return new TokenOfOwnerByIndexQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TokenURIQueryRequest CreateTokenURIQueryRequest()
        {
            return new TokenURIQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TotalSupplyQueryRequest CreateTotalSupplyQueryRequest()
        {
            return new TotalSupplyQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TransferFromTransactionRequest CreateTransferFromTransactionRequest()
        {
            return new TransferFromTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }

    }
}
