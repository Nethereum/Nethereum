using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.Circles.Contracts.INameRegistry.ContractDefinition
{


    public partial class INameRegistryDeployment : INameRegistryDeploymentBase
    {
        public INameRegistryDeployment() : base(BYTECODE) { }
        public INameRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class INameRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public INameRegistryDeploymentBase() : base(BYTECODE) { }
        public INameRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class GetMetadataDigestFunction : GetMetadataDigestFunctionBase { }

    [Function("getMetadataDigest", "bytes32")]
    public class GetMetadataDigestFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class IsValidNameFunction : IsValidNameFunctionBase { }

    [Function("isValidName", "bool")]
    public class IsValidNameFunctionBase : FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class IsValidSymbolFunction : IsValidSymbolFunctionBase { }

    [Function("isValidSymbol", "bool")]
    public class IsValidSymbolFunctionBase : FunctionMessage
    {
        [Parameter("string", "symbol", 1)]
        public virtual string Symbol { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class RegisterCustomNameFunction : RegisterCustomNameFunctionBase { }

    [Function("registerCustomName")]
    public class RegisterCustomNameFunctionBase : FunctionMessage
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("string", "name", 2)]
        public virtual string Name { get; set; }
    }

    public partial class RegisterCustomSymbolFunction : RegisterCustomSymbolFunctionBase { }

    [Function("registerCustomSymbol")]
    public class RegisterCustomSymbolFunctionBase : FunctionMessage
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("string", "symbol", 2)]
        public virtual string Symbol { get; set; }
    }

    public partial class SetMetadataDigestFunction : SetMetadataDigestFunctionBase { }

    [Function("setMetadataDigest")]
    public class SetMetadataDigestFunctionBase : FunctionMessage
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("bytes32", "metadataDigest", 2)]
        public virtual byte[] MetadataDigest { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class GetMetadataDigestOutputDTO : GetMetadataDigestOutputDTOBase { }

    [FunctionOutput]
    public class GetMetadataDigestOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class IsValidNameOutputDTO : IsValidNameOutputDTOBase { }

    [FunctionOutput]
    public class IsValidNameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidSymbolOutputDTO : IsValidSymbolOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }







    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
