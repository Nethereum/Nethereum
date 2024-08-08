using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Newtonsoft.Json;
using MyProject.Contracts.Standard_Token.ContractDefinition;

namespace MyProject.Contracts.Standard_Token
{
    public partial class StandardTokenContractRequestFactory 
    {
        public string ContractAddress { get; protected set; }
        public IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory { get; protected set; }
        public IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory { get; protected set; }
        public StandardTokenContractRequestFactory(string contractAddress, IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, IContractQueryUnityRequestFactory contractQueryUnityRequestFactory)
            {
                ContractAddress = contractAddress;
                ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
                ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
            }


        public AllowanceQueryRequest CreateAllowanceQueryRequest()
        {
            return new AllowanceQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public AllowedQueryRequest CreateAllowedQueryRequest()
        {
            return new AllowedQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public ApproveTransactionRequest CreateApproveTransactionRequest()
        {
            return new ApproveTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public BalanceOfQueryRequest CreateBalanceOfQueryRequest()
        {
            return new BalanceOfQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public BalancesQueryRequest CreateBalancesQueryRequest()
        {
            return new BalancesQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public DecimalsQueryRequest CreateDecimalsQueryRequest()
        {
            return new DecimalsQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public NameQueryRequest CreateNameQueryRequest()
        {
            return new NameQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public SymbolQueryRequest CreateSymbolQueryRequest()
        {
            return new SymbolQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TotalSupplyQueryRequest CreateTotalSupplyQueryRequest()
        {
            return new TotalSupplyQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public TransferTransactionRequest CreateTransferTransactionRequest()
        {
            return new TransferTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public TransferFromTransactionRequest CreateTransferFromTransactionRequest()
        {
            return new TransferFromTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }

    }
}
