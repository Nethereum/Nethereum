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
using Nethereum.ENS.EthRegistrarSubdomainRegistrar.ContractDefinition;

namespace Nethereum.ENS
{
    public partial class EthRegistrarSubdomainRegistrarService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, EthRegistrarSubdomainRegistrarDeployment ethRegistrarSubdomainRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<EthRegistrarSubdomainRegistrarDeployment>().SendRequestAndWaitForReceiptAsync(ethRegistrarSubdomainRegistrarDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, EthRegistrarSubdomainRegistrarDeployment ethRegistrarSubdomainRegistrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EthRegistrarSubdomainRegistrarDeployment>().SendRequestAsync(ethRegistrarSubdomainRegistrarDeployment);
        }

        public static async Task<EthRegistrarSubdomainRegistrarService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, EthRegistrarSubdomainRegistrarDeployment ethRegistrarSubdomainRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, ethRegistrarSubdomainRegistrarDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new EthRegistrarSubdomainRegistrarService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public EthRegistrarSubdomainRegistrarService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceID = interfaceID;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public Task<string> OwnerQueryAsync(byte[] label, BlockParameter blockParameter = null)
        {
            var ownerFunction = new OwnerFunction();
                ownerFunction.Label = label;
            
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        public Task<string> StopRequestAsync(StopFunction stopFunction)
        {
             return ContractHandler.SendRequestAsync(stopFunction);
        }

        public Task<string> StopRequestAsync()
        {
             return ContractHandler.SendRequestAsync<StopFunction>();
        }

        public Task<TransactionReceipt> StopRequestAndWaitForReceiptAsync(StopFunction stopFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(stopFunction, cancellationToken);
        }

        public Task<TransactionReceipt> StopRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<StopFunction>(null, cancellationToken);
        }

        public Task<string> MigrationQueryAsync(MigrationFunction migrationFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MigrationFunction, string>(migrationFunction, blockParameter);
        }

        
        public Task<string> MigrationQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MigrationFunction, string>(null, blockParameter);
        }

        public Task<string> RegistrarOwnerQueryAsync(RegistrarOwnerFunction registrarOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrarOwnerFunction, string>(registrarOwnerFunction, blockParameter);
        }

        
        public Task<string> RegistrarOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrarOwnerFunction, string>(null, blockParameter);
        }

        public Task<string> RegistrarQueryAsync(RegistrarFunction registrarFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrarFunction, string>(registrarFunction, blockParameter);
        }

        
        public Task<string> RegistrarQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrarFunction, string>(null, blockParameter);
        }

        public Task<QueryOutputDTO> QueryQueryAsync(QueryFunction queryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<QueryFunction, QueryOutputDTO>(queryFunction, blockParameter);
        }

        public Task<QueryOutputDTO> QueryQueryAsync(byte[] label, string subdomain, BlockParameter blockParameter = null)
        {
            var queryFunction = new QueryFunction();
                queryFunction.Label = label;
                queryFunction.Subdomain = subdomain;
            
            return ContractHandler.QueryDeserializingToObjectAsync<QueryFunction, QueryOutputDTO>(queryFunction, blockParameter);
        }

        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }

        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }

        public Task<string> RegisterRequestAsync(RegisterFunction registerFunction)
        {
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(RegisterFunction registerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> RegisterRequestAsync(byte[] label, string subdomain, string subdomainOwner, string referrer, string resolver)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Label = label;
                registerFunction.Subdomain = subdomain;
                registerFunction.SubdomainOwner = subdomainOwner;
                registerFunction.Referrer = referrer;
                registerFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(byte[] label, string subdomain, string subdomainOwner, string referrer, string resolver, CancellationTokenSource cancellationToken = null)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Label = label;
                registerFunction.Subdomain = subdomain;
                registerFunction.SubdomainOwner = subdomainOwner;
                registerFunction.Referrer = referrer;
                registerFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> SetMigrationAddressRequestAsync(SetMigrationAddressFunction setMigrationAddressFunction)
        {
             return ContractHandler.SendRequestAsync(setMigrationAddressFunction);
        }

        public Task<TransactionReceipt> SetMigrationAddressRequestAndWaitForReceiptAsync(SetMigrationAddressFunction setMigrationAddressFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMigrationAddressFunction, cancellationToken);
        }

        public Task<string> SetMigrationAddressRequestAsync(string migration)
        {
            var setMigrationAddressFunction = new SetMigrationAddressFunction();
                setMigrationAddressFunction.Migration = migration;
            
             return ContractHandler.SendRequestAsync(setMigrationAddressFunction);
        }

        public Task<TransactionReceipt> SetMigrationAddressRequestAndWaitForReceiptAsync(string migration, CancellationTokenSource cancellationToken = null)
        {
            var setMigrationAddressFunction = new SetMigrationAddressFunction();
                setMigrationAddressFunction.Migration = migration;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMigrationAddressFunction, cancellationToken);
        }

        public Task<BigInteger> RentDueQueryAsync(RentDueFunction rentDueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RentDueFunction, BigInteger>(rentDueFunction, blockParameter);
        }

        
        public Task<BigInteger> RentDueQueryAsync(byte[] label, string subdomain, BlockParameter blockParameter = null)
        {
            var rentDueFunction = new RentDueFunction();
                rentDueFunction.Label = label;
                rentDueFunction.Subdomain = subdomain;
            
            return ContractHandler.QueryAsync<RentDueFunction, BigInteger>(rentDueFunction, blockParameter);
        }

        public Task<string> SetResolverRequestAsync(SetResolverFunction setResolverFunction)
        {
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(SetResolverFunction setResolverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }

        public Task<string> SetResolverRequestAsync(string name, string resolver)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Name = name;
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(string name, string resolver, CancellationTokenSource cancellationToken = null)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Name = name;
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }

        public Task<bool> StoppedQueryAsync(StoppedFunction stoppedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StoppedFunction, bool>(stoppedFunction, blockParameter);
        }

        
        public Task<bool> StoppedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StoppedFunction, bool>(null, blockParameter);
        }

        public Task<byte[]> TLD_NODEQueryAsync(TLD_NODEFunction tLD_NODEFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TLD_NODEFunction, byte[]>(tLD_NODEFunction, blockParameter);
        }

        
        public Task<byte[]> TLD_NODEQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TLD_NODEFunction, byte[]>(null, blockParameter);
        }

        public Task<string> MigrateRequestAsync(MigrateFunction migrateFunction)
        {
             return ContractHandler.SendRequestAsync(migrateFunction);
        }

        public Task<TransactionReceipt> MigrateRequestAndWaitForReceiptAsync(MigrateFunction migrateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(migrateFunction, cancellationToken);
        }

        public Task<string> MigrateRequestAsync(string name)
        {
            var migrateFunction = new MigrateFunction();
                migrateFunction.Name = name;
            
             return ContractHandler.SendRequestAsync(migrateFunction);
        }

        public Task<TransactionReceipt> MigrateRequestAndWaitForReceiptAsync(string name, CancellationTokenSource cancellationToken = null)
        {
            var migrateFunction = new MigrateFunction();
                migrateFunction.Name = name;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(migrateFunction, cancellationToken);
        }

        public Task<string> PayRentRequestAsync(PayRentFunction payRentFunction)
        {
             return ContractHandler.SendRequestAsync(payRentFunction);
        }

        public Task<TransactionReceipt> PayRentRequestAndWaitForReceiptAsync(PayRentFunction payRentFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(payRentFunction, cancellationToken);
        }

        public Task<string> PayRentRequestAsync(byte[] label, string subdomain)
        {
            var payRentFunction = new PayRentFunction();
                payRentFunction.Label = label;
                payRentFunction.Subdomain = subdomain;
            
             return ContractHandler.SendRequestAsync(payRentFunction);
        }

        public Task<TransactionReceipt> PayRentRequestAndWaitForReceiptAsync(byte[] label, string subdomain, CancellationTokenSource cancellationToken = null)
        {
            var payRentFunction = new PayRentFunction();
                payRentFunction.Label = label;
                payRentFunction.Subdomain = subdomain;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(payRentFunction, cancellationToken);
        }

        public Task<string> ConfigureDomainForRequestAsync(ConfigureDomainForFunction configureDomainForFunction)
        {
             return ContractHandler.SendRequestAsync(configureDomainForFunction);
        }

        public Task<TransactionReceipt> ConfigureDomainForRequestAndWaitForReceiptAsync(ConfigureDomainForFunction configureDomainForFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(configureDomainForFunction, cancellationToken);
        }

        public Task<string> ConfigureDomainForRequestAsync(string name, BigInteger price, BigInteger referralFeePPM, string owner, string transfer)
        {
            var configureDomainForFunction = new ConfigureDomainForFunction();
                configureDomainForFunction.Name = name;
                configureDomainForFunction.Price = price;
                configureDomainForFunction.ReferralFeePPM = referralFeePPM;
                configureDomainForFunction.Owner = owner;
                configureDomainForFunction.Transfer = transfer;
            
             return ContractHandler.SendRequestAsync(configureDomainForFunction);
        }

        public Task<TransactionReceipt> ConfigureDomainForRequestAndWaitForReceiptAsync(string name, BigInteger price, BigInteger referralFeePPM, string owner, string transfer, CancellationTokenSource cancellationToken = null)
        {
            var configureDomainForFunction = new ConfigureDomainForFunction();
                configureDomainForFunction.Name = name;
                configureDomainForFunction.Price = price;
                configureDomainForFunction.ReferralFeePPM = referralFeePPM;
                configureDomainForFunction.Owner = owner;
                configureDomainForFunction.Transfer = transfer;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(configureDomainForFunction, cancellationToken);
        }

        public Task<string> ConfigureDomainRequestAsync(ConfigureDomainFunction configureDomainFunction)
        {
             return ContractHandler.SendRequestAsync(configureDomainFunction);
        }

        public Task<TransactionReceipt> ConfigureDomainRequestAndWaitForReceiptAsync(ConfigureDomainFunction configureDomainFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(configureDomainFunction, cancellationToken);
        }

        public Task<string> ConfigureDomainRequestAsync(string name, BigInteger price, BigInteger referralFeePPM)
        {
            var configureDomainFunction = new ConfigureDomainFunction();
                configureDomainFunction.Name = name;
                configureDomainFunction.Price = price;
                configureDomainFunction.ReferralFeePPM = referralFeePPM;
            
             return ContractHandler.SendRequestAsync(configureDomainFunction);
        }

        public Task<TransactionReceipt> ConfigureDomainRequestAndWaitForReceiptAsync(string name, BigInteger price, BigInteger referralFeePPM, CancellationTokenSource cancellationToken = null)
        {
            var configureDomainFunction = new ConfigureDomainFunction();
                configureDomainFunction.Name = name;
                configureDomainFunction.Price = price;
                configureDomainFunction.ReferralFeePPM = referralFeePPM;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(configureDomainFunction, cancellationToken);
        }

        public Task<string> UnlistDomainRequestAsync(UnlistDomainFunction unlistDomainFunction)
        {
             return ContractHandler.SendRequestAsync(unlistDomainFunction);
        }

        public Task<TransactionReceipt> UnlistDomainRequestAndWaitForReceiptAsync(UnlistDomainFunction unlistDomainFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlistDomainFunction, cancellationToken);
        }

        public Task<string> UnlistDomainRequestAsync(string name)
        {
            var unlistDomainFunction = new UnlistDomainFunction();
                unlistDomainFunction.Name = name;
            
             return ContractHandler.SendRequestAsync(unlistDomainFunction);
        }

        public Task<TransactionReceipt> UnlistDomainRequestAndWaitForReceiptAsync(string name, CancellationTokenSource cancellationToken = null)
        {
            var unlistDomainFunction = new UnlistDomainFunction();
                unlistDomainFunction.Name = name;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unlistDomainFunction, cancellationToken);
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

        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public Task<string> TransferRequestAsync(string name, string newOwner)
        {
            var transferFunction = new TransferFunction();
                transferFunction.Name = name;
                transferFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string name, string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.Name = name;
                transferFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }
    }
}
