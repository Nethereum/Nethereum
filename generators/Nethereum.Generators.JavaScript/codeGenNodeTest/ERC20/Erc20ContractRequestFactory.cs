using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Newtonsoft.Json;
using Nethereum.Unity.Contracts.Standards.ERC20.ContractDefinition;

namespace Nethereum.Unity.Contracts.Standards.ERC20
{
    public partial class Erc20ContractRequestFactory 
    {
        public string ContractAddress { get; protected set; }
        public IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory { get; protected set; }
        public IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory { get; protected set; }
        public Erc20ContractRequestFactory(string contractAddress, IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, IContractQueryUnityRequestFactory contractQueryUnityRequestFactory)
            {
                ContractAddress = contractAddress;
                ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
                ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
            }


        public DomainSeparatorQueryRequest CreateDomainSeparatorQueryRequest()
        {
            return new DomainSeparatorQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public AllowanceQueryRequest CreateAllowanceQueryRequest()
        {
            return new AllowanceQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public ApproveTransactionRequest CreateApproveTransactionRequest()
        {
            return new ApproveTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
        }


        public BalanceOfQueryRequest CreateBalanceOfQueryRequest()
        {
            return new BalanceOfQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public DecimalsQueryRequest CreateDecimalsQueryRequest()
        {
            return new DecimalsQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public Eip712DomainQueryRequest CreateEip712DomainQueryRequest()
        {
            return new Eip712DomainQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public NameQueryRequest CreateNameQueryRequest()
        {
            return new NameQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public NoncesQueryRequest CreateNoncesQueryRequest()
        {
            return new NoncesQueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
        }


        public PermitTransactionRequest CreatePermitTransactionRequest()
        {
            return new PermitTransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
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
