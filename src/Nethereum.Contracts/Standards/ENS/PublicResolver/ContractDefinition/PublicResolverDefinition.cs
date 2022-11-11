using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition
{
    public partial class ABIFunction : ABIFunctionBase { }

    [Function("ABI", typeof(ABIOutputDTO))]
    public class ABIFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint256", "contentTypes", 2)]
        public virtual BigInteger ContentTypes { get; set; }
    }

    public partial class AddrFunction : AddrFunctionBase { }

    [Function("addr", "address")]
    public class AddrFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class AddrFunction2 : AddrFunctionBase2 { }

    [Function("addr", "bytes")]
    public class AddrFunctionBase2 : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint256", "coinType", 2)]
        public virtual BigInteger CoinType { get; set; }
    }

    public partial class AuthorisationsFunction : AuthorisationsFunctionBase { }

    [Function("authorisations", "bool")]
    public class AuthorisationsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
    }

    public partial class ClearDNSZoneFunction : ClearDNSZoneFunctionBase { }

    [Function("clearDNSZone")]
    public class ClearDNSZoneFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class ContenthashFunction : ContenthashFunctionBase { }

    [Function("contenthash", "bytes")]
    public class ContenthashFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class DnsRecordFunction : DnsRecordFunctionBase { }

    [Function("dnsRecord", "bytes")]
    public class DnsRecordFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "name", 2)]
        public virtual byte[] Name { get; set; }
        [Parameter("uint16", "resource", 3)]
        public virtual ushort Resource { get; set; }
    }

    public partial class HasDNSRecordsFunction : HasDNSRecordsFunctionBase { }

    [Function("hasDNSRecords", "bool")]
    public class HasDNSRecordsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "name", 2)]
        public virtual byte[] Name { get; set; }
    }

    public partial class InterfaceImplementerFunction : InterfaceImplementerFunctionBase { }

    [Function("interfaceImplementer", "address")]
    public class InterfaceImplementerFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes4", "interfaceID", 2)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class PubkeyFunction : PubkeyFunctionBase { }

    [Function("pubkey", typeof(PubkeyOutputDTO))]
    public class PubkeyFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
    }

    public partial class SetABIFunction : SetABIFunctionBase { }

    [Function("setABI")]
    public class SetABIFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint256", "contentType", 2)]
        public virtual BigInteger ContentType { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SetAddrFunction : SetAddrFunctionBase { }

    [Function("setAddr")]
    public class SetAddrFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint256", "coinType", 2)]
        public virtual BigInteger CoinType { get; set; }
        [Parameter("bytes", "a", 3)]
        public virtual byte[] A { get; set; }
    }

    public partial class SetAddrFunction2 : SetAddrFunctionBase2 { }

    [Function("setAddr")]
    public class SetAddrFunctionBase2 : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "a", 2)]
        public virtual string A { get; set; }
    }

    public partial class SetAuthorisationFunction : SetAuthorisationFunctionBase { }

    [Function("setAuthorisation")]
    public class SetAuthorisationFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "target", 2)]
        public virtual string Target { get; set; }
        [Parameter("bool", "isAuthorised", 3)]
        public virtual bool IsAuthorised { get; set; }
    }

    public partial class SetContenthashFunction : SetContenthashFunctionBase { }

    [Function("setContenthash")]
    public class SetContenthashFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes", "hash", 2)]
        public virtual byte[] Hash { get; set; }
    }

    public partial class SetDNSRecordsFunction : SetDNSRecordsFunctionBase { }

    [Function("setDNSRecords")]
    public class SetDNSRecordsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SetInterfaceFunction : SetInterfaceFunctionBase { }

    [Function("setInterface")]
    public class SetInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes4", "interfaceID", 2)]
        public virtual byte[] InterfaceID { get; set; }
        [Parameter("address", "implementer", 3)]
        public virtual string Implementer { get; set; }
    }

    public partial class SetNameFunction : SetNameFunctionBase { }

    [Function("setName")]
    public class SetNameFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("string", "name", 2)]
        public virtual string Name { get; set; }
    }

    public partial class SetPubkeyFunction : SetPubkeyFunctionBase { }

    [Function("setPubkey")]
    public class SetPubkeyFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "x", 2)]
        public virtual byte[] X { get; set; }
        [Parameter("bytes32", "y", 3)]
        public virtual byte[] Y { get; set; }
    }

    public partial class SetTextFunction : SetTextFunctionBase { }

    [Function("setText")]
    public class SetTextFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("string", "key", 2)]
        public virtual string Key { get; set; }
        [Parameter("string", "value", 3)]
        public virtual string Value { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class TextFunction : TextFunctionBase { }

    [Function("text", "string")]
    public class TextFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "node", 1)]
        public virtual byte[] Node { get; set; }
        [Parameter("string", "key", 2)]
        public virtual string Key { get; set; }
    }

    public partial class ABIChangedEventDTO : ABIChangedEventDTOBase { }

    [Event("ABIChanged")]
    public class ABIChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint256", "contentType", 2, true)]
        public virtual BigInteger ContentType { get; set; }
    }

    public partial class AddrChangedEventDTO : AddrChangedEventDTOBase { }

    [Event("AddrChanged")]
    public class AddrChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "a", 2, false)]
        public virtual string A { get; set; }
    }

    public partial class AddressChangedEventDTO : AddressChangedEventDTOBase { }

    [Event("AddressChanged")]
    public class AddressChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("uint256", "coinType", 2, false)]
        public virtual BigInteger CoinType { get; set; }
        [Parameter("bytes", "newAddress", 3, false)]
        public virtual byte[] NewAddress { get; set; }
    }

    public partial class AuthorisationChangedEventDTO : AuthorisationChangedEventDTOBase { }

    [Event("AuthorisationChanged")]
    public class AuthorisationChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("address", "owner", 2, true)]
        public virtual string Owner { get; set; }
        [Parameter("address", "target", 3, true)]
        public virtual string Target { get; set; }
        [Parameter("bool", "isAuthorised", 4, false)]
        public virtual bool IsAuthorised { get; set; }
    }

    public partial class ContenthashChangedEventDTO : ContenthashChangedEventDTOBase { }

    [Event("ContenthashChanged")]
    public class ContenthashChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes", "hash", 2, false)]
        public virtual byte[] Hash { get; set; }
    }

    public partial class DNSRecordChangedEventDTO : DNSRecordChangedEventDTOBase { }

    [Event("DNSRecordChanged")]
    public class DNSRecordChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes", "name", 2, false)]
        public virtual byte[] Name { get; set; }
        [Parameter("uint16", "resource", 3, false)]
        public virtual ushort Resource { get; set; }
        [Parameter("bytes", "record", 4, false)]
        public virtual byte[] Record { get; set; }
    }

    public partial class DNSRecordDeletedEventDTO : DNSRecordDeletedEventDTOBase { }

    [Event("DNSRecordDeleted")]
    public class DNSRecordDeletedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes", "name", 2, false)]
        public virtual byte[] Name { get; set; }
        [Parameter("uint16", "resource", 3, false)]
        public virtual ushort Resource { get; set; }
    }

    public partial class DNSZoneClearedEventDTO : DNSZoneClearedEventDTOBase { }

    [Event("DNSZoneCleared")]
    public class DNSZoneClearedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
    }

    public partial class InterfaceChangedEventDTO : InterfaceChangedEventDTOBase { }

    [Event("InterfaceChanged")]
    public class InterfaceChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes4", "interfaceID", 2, true)]
        public virtual byte[] InterfaceID { get; set; }
        [Parameter("address", "implementer", 3, false)]
        public virtual string Implementer { get; set; }
    }

    public partial class NameChangedEventDTO : NameChangedEventDTOBase { }

    [Event("NameChanged")]
    public class NameChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("string", "name", 2, false)]
        public virtual string Name { get; set; }
    }

    public partial class PubkeyChangedEventDTO : PubkeyChangedEventDTOBase { }

    [Event("PubkeyChanged")]
    public class PubkeyChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("bytes32", "x", 2, false)]
        public virtual byte[] X { get; set; }
        [Parameter("bytes32", "y", 3, false)]
        public virtual byte[] Y { get; set; }
    }

    public partial class TextChangedEventDTO : TextChangedEventDTOBase { }

    [Event("TextChanged")]
    public class TextChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "node", 1, true)]
        public virtual byte[] Node { get; set; }
        [Parameter("string", "indexedKey", 2, true)]
        public virtual string IndexedKey { get; set; }
        [Parameter("string", "key", 3, false)]
        public virtual string Key { get; set; }
    }

    public partial class ABIOutputDTO : ABIOutputDTOBase { }

    [FunctionOutput]
    public class ABIOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
        [Parameter("bytes", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
    }

    public partial class AddrOutputDTO : AddrOutputDTOBase { }

    [FunctionOutput]
    public class AddrOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AddrOutputDTO2 : AddrOutputDTOBase2 { }

    [FunctionOutput]
    public class AddrOutputDTOBase2 : IFunctionOutputDTO
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AuthorisationsOutputDTO : AuthorisationsOutputDTOBase { }

    [FunctionOutput]
    public class AuthorisationsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class ContenthashOutputDTO : ContenthashOutputDTOBase { }

    [FunctionOutput]
    public class ContenthashOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class DnsRecordOutputDTO : DnsRecordOutputDTOBase { }

    [FunctionOutput]
    public class DnsRecordOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class HasDNSRecordsOutputDTO : HasDNSRecordsOutputDTOBase { }

    [FunctionOutput]
    public class HasDNSRecordsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class InterfaceImplementerOutputDTO : InterfaceImplementerOutputDTOBase { }

    [FunctionOutput]
    public class InterfaceImplementerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class PubkeyOutputDTO : PubkeyOutputDTOBase { }

    [FunctionOutput]
    public class PubkeyOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "x", 1)]
        public virtual byte[] X { get; set; }
        [Parameter("bytes32", "y", 2)]
        public virtual byte[] Y { get; set; }
    }


    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class TextOutputDTO : TextOutputDTOBase { }

    [FunctionOutput]
    public class TextOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
