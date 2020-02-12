using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.ENS.EthRegistrarSubdomainRegistrar.ContractDefinition
{
    public partial class EthRegistrarSubdomainRegistrarDeployment : EthRegistrarSubdomainRegistrarDeploymentBase
    {
        public EthRegistrarSubdomainRegistrarDeployment() : base(BYTECODE) { }
        public EthRegistrarSubdomainRegistrarDeployment(string byteCode) : base(byteCode) { }
    }

    public class EthRegistrarSubdomainRegistrarDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public EthRegistrarSubdomainRegistrarDeploymentBase() : base(BYTECODE) { }
        public EthRegistrarSubdomainRegistrarDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "ens", 1)]
        public virtual string Ens { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
    }

    public partial class StopFunction : StopFunctionBase { }

    [Function("stop")]
    public class StopFunctionBase : FunctionMessage
    {

    }

    public partial class MigrationFunction : MigrationFunctionBase { }

    [Function("migration", "address")]
    public class MigrationFunctionBase : FunctionMessage
    {

    }

    public partial class RegistrarOwnerFunction : RegistrarOwnerFunctionBase { }

    [Function("registrarOwner", "address")]
    public class RegistrarOwnerFunctionBase : FunctionMessage
    {

    }

    public partial class RegistrarFunction : RegistrarFunctionBase { }

    [Function("registrar", "address")]
    public class RegistrarFunctionBase : FunctionMessage
    {

    }

    public partial class QueryFunction : QueryFunctionBase { }

    [Function("query", typeof(QueryOutputDTO))]
    public class QueryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "subdomain", 2)]
        public virtual string Subdomain { get; set; }
    }

    public partial class EnsFunction : EnsFunctionBase { }

    [Function("ens", "address")]
    public class EnsFunctionBase : FunctionMessage
    {

    }

    public partial class RegisterFunction : RegisterFunctionBase { }

    [Function("register")]
    public class RegisterFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "subdomain", 2)]
        public virtual string Subdomain { get; set; }
        [Parameter("address", "_subdomainOwner", 3)]
        public virtual string SubdomainOwner { get; set; }
        [Parameter("address", "referrer", 4)]
        public virtual string Referrer { get; set; }
        [Parameter("address", "resolver", 5)]
        public virtual string Resolver { get; set; }
    }

    public partial class SetMigrationAddressFunction : SetMigrationAddressFunctionBase { }

    [Function("setMigrationAddress")]
    public class SetMigrationAddressFunctionBase : FunctionMessage
    {
        [Parameter("address", "_migration", 1)]
        public virtual string Migration { get; set; }
    }

    public partial class RentDueFunction : RentDueFunctionBase { }

    [Function("rentDue", "uint256")]
    public class RentDueFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "subdomain", 2)]
        public virtual string Subdomain { get; set; }
    }

    public partial class SetResolverFunction : SetResolverFunctionBase { }

    [Function("setResolver")]
    public class SetResolverFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "resolver", 2)]
        public virtual string Resolver { get; set; }
    }

    public partial class StoppedFunction : StoppedFunctionBase { }

    [Function("stopped", "bool")]
    public class StoppedFunctionBase : FunctionMessage
    {

    }

    public partial class TLD_NODEFunction : TLD_NODEFunctionBase { }

    [Function("TLD_NODE", "bytes32")]
    public class TLD_NODEFunctionBase : FunctionMessage
    {

    }

    public partial class MigrateFunction : MigrateFunctionBase { }

    [Function("migrate")]
    public class MigrateFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class PayRentFunction : PayRentFunctionBase { }

    [Function("payRent")]
    public class PayRentFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "label", 1)]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "subdomain", 2)]
        public virtual string Subdomain { get; set; }
    }

    public partial class ConfigureDomainForFunction : ConfigureDomainForFunctionBase { }

    [Function("configureDomainFor")]
    public class ConfigureDomainForFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("uint256", "price", 2)]
        public virtual BigInteger Price { get; set; }
        [Parameter("uint256", "referralFeePPM", 3)]
        public virtual BigInteger ReferralFeePPM { get; set; }
        [Parameter("address", "_owner", 4)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_transfer", 5)]
        public virtual string Transfer { get; set; }
    }

    public partial class ConfigureDomainFunction : ConfigureDomainFunctionBase { }

    [Function("configureDomain")]
    public class ConfigureDomainFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("uint256", "price", 2)]
        public virtual BigInteger Price { get; set; }
        [Parameter("uint256", "referralFeePPM", 3)]
        public virtual BigInteger ReferralFeePPM { get; set; }
    }

    public partial class UnlistDomainFunction : UnlistDomainFunctionBase { }

    [Function("unlistDomain")]
    public class UnlistDomainFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class TransferFunction : TransferFunctionBase { }

    [Function("transfer")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "newOwner", 2)]
        public virtual string NewOwner { get; set; }
    }

    public partial class DomainTransferredEventDTO : DomainTransferredEventDTOBase { }

    [Event("DomainTransferred")]
    public class DomainTransferredEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "name", 2, false )]
        public virtual string Name { get; set; }
    }

    public partial class OwnerChangedEventDTO : OwnerChangedEventDTOBase { }

    [Event("OwnerChanged")]
    public class OwnerChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("address", "oldOwner", 2, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 3, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class DomainConfiguredEventDTO : DomainConfiguredEventDTOBase { }

    [Event("DomainConfigured")]
    public class DomainConfiguredEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
    }

    public partial class DomainUnlistedEventDTO : DomainUnlistedEventDTOBase { }

    [Event("DomainUnlisted")]
    public class DomainUnlistedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
    }

    public partial class NewRegistrationEventDTO : NewRegistrationEventDTOBase { }

    [Event("NewRegistration")]
    public class NewRegistrationEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "subdomain", 2, false )]
        public virtual string Subdomain { get; set; }
        [Parameter("address", "owner", 3, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "referrer", 4, true )]
        public virtual string Referrer { get; set; }
        [Parameter("uint256", "price", 5, false )]
        public virtual BigInteger Price { get; set; }
    }

    public partial class RentPaidEventDTO : RentPaidEventDTOBase { }

    [Event("RentPaid")]
    public class RentPaidEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "label", 1, true )]
        public virtual byte[] Label { get; set; }
        [Parameter("string", "subdomain", 2, false )]
        public virtual string Subdomain { get; set; }
        [Parameter("uint256", "amount", 3, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "expirationDate", 4, false )]
        public virtual BigInteger ExpirationDate { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class MigrationOutputDTO : MigrationOutputDTOBase { }

    [FunctionOutput]
    public class MigrationOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RegistrarOwnerOutputDTO : RegistrarOwnerOutputDTOBase { }

    [FunctionOutput]
    public class RegistrarOwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RegistrarOutputDTO : RegistrarOutputDTOBase { }

    [FunctionOutput]
    public class RegistrarOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class QueryOutputDTO : QueryOutputDTOBase { }

    [FunctionOutput]
    public class QueryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "domain", 1)]
        public virtual string Domain { get; set; }
        [Parameter("uint256", "price", 2)]
        public virtual BigInteger Price { get; set; }
        [Parameter("uint256", "rent", 3)]
        public virtual BigInteger Rent { get; set; }
        [Parameter("uint256", "referralFeePPM", 4)]
        public virtual BigInteger ReferralFeePPM { get; set; }
    }

    public partial class EnsOutputDTO : EnsOutputDTOBase { }

    [FunctionOutput]
    public class EnsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class RentDueOutputDTO : RentDueOutputDTOBase { }

    [FunctionOutput]
    public class RentDueOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "timestamp", 1)]
        public virtual BigInteger Timestamp { get; set; }
    }



    public partial class StoppedOutputDTO : StoppedOutputDTOBase { }

    [FunctionOutput]
    public class StoppedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class TLD_NODEOutputDTO : TLD_NODEOutputDTOBase { }

    [FunctionOutput]
    public class TLD_NODEOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }














}
