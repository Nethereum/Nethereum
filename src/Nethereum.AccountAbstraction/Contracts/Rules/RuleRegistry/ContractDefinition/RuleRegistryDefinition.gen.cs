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
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition
{


    public partial class RuleRegistryDeployment : RuleRegistryDeploymentBase
    {
        public RuleRegistryDeployment() : base(BYTECODE) { }
        public RuleRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class RuleRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608080604052346026575f80546001600160a01b0319163317905561042b908161002b8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f3560e01c90816375f5632d146103a5575080637dc673bc1461034c5780638da5cb5b146103255780639f9a093c14610301578063dafdd858146102105763e9ae5c531461005e575f80fd5b346101de5760403660031901126101de5760243567ffffffffffffffff81116101de57366023820112156101de5780600401359067ffffffffffffffff82116101de5736602483830101116101de576004355f908152600160205260409020546001600160a01b0316908115610201575f9160246044859360405196879586948593631658af3760e01b855260206004860152828286015201848401378181018301879052601f01601f191681010301915afa9081156101f6575f91610150575b6020604083815192839181835280519182918282860152018484015e5f828201840152601f01601f19168101030190f35b90503d805f833e61016181836103d3565b8101906020818303126101de5780519067ffffffffffffffff82116101de570181601f820112156101de5780519167ffffffffffffffff83116101e257604051906101b6601f8501601f1916602001836103d3565b838252602084840101116101de575f602084819582604096018386015e83010152915061011f565b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b6040513d5f823e3d90fd5b6338c7728d60e01b5f5260045ffd5b346101de5760403660031901126101de576004356024356001600160a01b038116908190036101de575f546001600160a01b03811633036102f25760a01c60ff166102e35780156102d4575f828152600160205260409020546001600160a01b03166102c55760207f7d13fa8ab1850b9b4644e103f897f7814db9a857e44a129f03ca0b830dae1cb991835f526001825260405f20816bffffffffffffffffffffffff60a01b825416179055604051908152a2005b63af15015b60e01b5f5260045ffd5b631491c71560e11b5f5260045ffd5b631afc1d2360e11b5f5260045ffd5b6330cd747160e01b5f5260045ffd5b346101de575f3660031901126101de57602060ff5f5460a01c166040519015158152f35b346101de575f3660031901126101de575f546040516001600160a01b039091168152602090f35b346101de575f3660031901126101de575f546001600160a01b03811633036102f25760ff60a01b1916600160a01b175f9081557f2fc209f3df2fb10c5fcb1620d3e5f067b9ef99813ca8fa6b24de0418bdac54209080a1005b346101de5760203660031901126101de576020906004355f526001825260018060a01b0360405f2054168152f35b90601f8019910116810190811067ffffffffffffffff8211176101e25760405256fea2646970667358221220a4e4cd199e461ceca5b1439e332da96bef86a0ae3165dfaf655b1a198bfd037d64736f6c634300081c0033";
        public RuleRegistryDeploymentBase() : base(BYTECODE) { }
        public RuleRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CloseRegistrationFunction : CloseRegistrationFunctionBase { }

    [Function("closeRegistration")]
    public class CloseRegistrationFunctionBase : FunctionMessage
    {

    }

    public partial class ExecuteFunction : ExecuteFunctionBase { }

    [Function("execute", "bytes")]
    public class ExecuteFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ruleId", 1)]
        public virtual byte[] RuleId { get; set; }
        [Parameter("bytes", "input", 2)]
        public virtual byte[] Input { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class RegisterRuleFunction : RegisterRuleFunctionBase { }

    [Function("registerRule")]
    public class RegisterRuleFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ruleId", 1)]
        public virtual byte[] RuleId { get; set; }
        [Parameter("address", "rule", 2)]
        public virtual string Rule { get; set; }
    }

    public partial class RegistrationClosedFunction : RegistrationClosedFunctionBase { }

    [Function("registrationClosed", "bool")]
    public class RegistrationClosedFunctionBase : FunctionMessage
    {

    }

    public partial class RuleOfFunction : RuleOfFunctionBase { }

    [Function("ruleOf", "address")]
    public class RuleOfFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }



    public partial class ExecuteOutputDTO : ExecuteOutputDTOBase { }

    [FunctionOutput]
    public class ExecuteOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "output", 1)]
        public virtual byte[] Output { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class RegistrationClosedOutputDTO : RegistrationClosedOutputDTOBase { }

    [FunctionOutput]
    public class RegistrationClosedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class RuleOfOutputDTO : RuleOfOutputDTOBase { }

    [FunctionOutput]
    public class RuleOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RegistrationClosedEventDTO : RegistrationClosedEventDTOBase { }

    [Event("RegistrationClosed")]
    public class RegistrationClosedEventDTOBase : IEventDTO
    {
    }

    public partial class RuleRegisteredEventDTO : RuleRegisteredEventDTOBase { }

    [Event("RuleRegistered")]
    public class RuleRegisteredEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "ruleId", 1, true )]
        public virtual byte[] RuleId { get; set; }
        [Parameter("address", "rule", 2, false )]
        public virtual string Rule { get; set; }
    }

    public partial class NotOwnerError : NotOwnerErrorBase { }
    [Error("NotOwner")]
    public class NotOwnerErrorBase : IErrorDTO
    {
    }

    public partial class RegistrationIsClosedError : RegistrationIsClosedErrorBase { }
    [Error("RegistrationIsClosed")]
    public class RegistrationIsClosedErrorBase : IErrorDTO
    {
    }

    public partial class RuleAlreadySetError : RuleAlreadySetErrorBase { }
    [Error("RuleAlreadySet")]
    public class RuleAlreadySetErrorBase : IErrorDTO
    {
    }

    public partial class UnknownRuleError : UnknownRuleErrorBase { }
    [Error("UnknownRule")]
    public class UnknownRuleErrorBase : IErrorDTO
    {
    }

    public partial class ZeroRuleError : ZeroRuleErrorBase { }
    [Error("ZeroRule")]
    public class ZeroRuleErrorBase : IErrorDTO
    {
    }
}
