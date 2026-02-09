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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition
{


    public partial class SudoPolicyDeployment : SudoPolicyDeploymentBase
    {
        public SudoPolicyDeployment() : base(BYTECODE) { }
        public SudoPolicyDeployment(string byteCode) : base(byteCode) { }
    }

    public class SudoPolicyDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608080604052346015576103cd908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806301ffc9a71461006457806305c008951461005f5780637129edce1461005a578063989c9e46146100555763cbf3450514610050575f80fd5b610299565b610204565b6101c7565b610178565b346101045760203660031901126101045760043563ffffffff60e01b811680910361010457633894f6e760e11b81149081156100f3575b81156100e2575b81156100d1575b81156100c0575b501515608052607f1960a0016080f35b634c4e4f2360e11b149050816100b0565b6301ffc9a760e01b811491506100a9565b63cbf3450560e01b811491506100a2565b6305c0089560e01b8114915061009b565b5f80fd5b602435906001600160a01b038216820361010457565b604435906001600160a01b038216820361010457565b600435906001600160a01b038216820361010457565b9181601f840112156101045782359167ffffffffffffffff8311610104576020838186019501011161010457565b346101045760a036600319011261010457610191610108565b5061019a61011e565b5060843567ffffffffffffffff8111610104576101bb90369060040161014a565b505060206040515f8152f35b346101045760403660031901126101045760243567ffffffffffffffff8111610104576101209060031990360301126101045760206040515f8152f35b346101045760603660031901126101045761021d610134565b60443560243567ffffffffffffffff8211610104577f5d14f8bf6f75758495bb0b0768b81cdebc7869d1f19edacc2f483ca0c89a171592610264606093369060040161014a565b5050335f52600160205261027c828260405f206102e9565b5060405191825233602083015260018060a01b03166040820152a1005b346101045760a0366003190112610104576102b2610108565b506102bb61011e565b5060843567ffffffffffffffff8111610104576102dc90369060040161014a565b5050602060405160018152f35b916001830192815f52836020526103138360405f209060018060a01b03165f5260205260405f2090565b5461038f57825f528060205260405f208054607f811161037f578461036493836001848882610356976103799c9b99010155019055905f5260205260405f205490565b94905f5260205260405f2090565b9060018060a01b03165f5260205260405f2090565b55600190565b638277484f5f526020526024601cfd5b505050505f9056fea2646970667358221220a37864f32f91cab8df045f01a73ac1e804b1cd764d6cdf7a573012722e05b48464736f6c634300081c0033";
        public SudoPolicyDeploymentBase() : base(BYTECODE) { }
        public SudoPolicyDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class Check1271SignedActionFunction : Check1271SignedActionFunctionBase { }

    [Function("check1271SignedAction", "bool")]
    public class Check1271SignedActionFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "requestSender", 2)]
        public virtual string RequestSender { get; set; }
        [Parameter("address", "account", 3)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "hash", 4)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "signature", 5)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class CheckActionFunction : CheckActionFunctionBase { }

    [Function("checkAction", "uint256")]
    public class CheckActionFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class CheckUserOpPolicyFunction : CheckUserOpPolicyFunctionBase { }

    [Function("checkUserOpPolicy", "uint256")]
    public class CheckUserOpPolicyFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
        [Parameter("tuple", "", 2)]
        public virtual PackedUserOperation ReturnValue2 { get; set; }
    }

    public partial class InitializeWithMultiplexerFunction : InitializeWithMultiplexerFunctionBase { }

    [Function("initializeWithMultiplexer")]
    public class InitializeWithMultiplexerFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "configId", 2)]
        public virtual byte[] ConfigId { get; set; }
        [Parameter("bytes", "", 3)]
        public virtual byte[] ReturnValue3 { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class Check1271SignedActionOutputDTO : Check1271SignedActionOutputDTOBase { }

    [FunctionOutput]
    public class Check1271SignedActionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class CheckActionOutputDTO : CheckActionOutputDTOBase { }

    [FunctionOutput]
    public class CheckActionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class CheckUserOpPolicyOutputDTO : CheckUserOpPolicyOutputDTOBase { }

    [FunctionOutput]
    public class CheckUserOpPolicyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class PolicySetEventDTO : PolicySetEventDTOBase { }

    [Event("PolicySet")]
    public class PolicySetEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, false )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "multiplexer", 2, false )]
        public virtual string Multiplexer { get; set; }
        [Parameter("address", "account", 3, false )]
        public virtual string Account { get; set; }
    }

    public partial class SudoPolicyInstalledMultiplexerEventDTO : SudoPolicyInstalledMultiplexerEventDTOBase { }

    [Event("SudoPolicyInstalledMultiplexer")]
    public class SudoPolicyInstalledMultiplexerEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "multiplexer", 2, true )]
        public virtual string Multiplexer { get; set; }
        [Parameter("bytes32", "id", 3, true )]
        public virtual byte[] Id { get; set; }
    }

    public partial class SudoPolicyRemovedEventDTO : SudoPolicyRemovedEventDTOBase { }

    [Event("SudoPolicyRemoved")]
    public class SudoPolicyRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "multiplexer", 2, true )]
        public virtual string Multiplexer { get; set; }
        [Parameter("bytes32", "id", 3, true )]
        public virtual byte[] Id { get; set; }
    }

    public partial class SudoPolicySetEventDTO : SudoPolicySetEventDTOBase { }

    [Event("SudoPolicySet")]
    public class SudoPolicySetEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "multiplexer", 2, true )]
        public virtual string Multiplexer { get; set; }
        [Parameter("bytes32", "id", 3, true )]
        public virtual byte[] Id { get; set; }
    }

    public partial class SudoPolicyUninstalledAllAccountEventDTO : SudoPolicyUninstalledAllAccountEventDTOBase { }

    [Event("SudoPolicyUninstalledAllAccount")]
    public class SudoPolicyUninstalledAllAccountEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }
}
