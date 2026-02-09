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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition
{


    public partial class UniActionPolicyDeployment : UniActionPolicyDeploymentBase
    {
        public UniActionPolicyDeployment() : base(BYTECODE) { }
        public UniActionPolicyDeployment(string byteCode) : base(byteCode) { }
    }

    public class UniActionPolicyDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080806040523460155761095e908161001a8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f3560e01c90816301ffc9a71461051d5750806305c00895146104c1578063989c9e46146102035763cc39a98514610048575f80fd5b346101ff5760603660031901126101ff57610061610588565b61006961059e565b6004355f908152602081815260408083206001600160a01b039586168452825280832093909416825291909152208054906100a26105e1565b600182015481526040519161020083018381106001600160401b038211176101eb576040525f90600201835b6010831061017557505050602081019182526040519283525160208301525190604081015f905b6010821061010357610c4083f35b835180516007811015610161578260206080819460c094600197526001600160401b0383820151168385015260408101511515604085015260608101516060850152015180516080840152015160a0820152019401910190926100f5565b634e487b7160e01b5f52602160045260245ffd5b61017d610600565b90825460ff8116916007831015610161578360ff602093600495600197526001600160401b038160081c168584015260481c16151560408201528486015460608201526101c86105e1565b6002870154815260038701548482015260808201528152019201920191906100ce565b634e487b7160e01b5f52604160045260245ffd5b5f80fd5b346101ff5760603660031901126101ff576004356001600160a01b038116908190036101ff57602435906044356001600160401b0381116101ff5761024c9036906004016105b4565b8101928184039291610c4084126101ff57610c206102686105e1565b8235815294601f1901126101ff5761027e6105e1565b946020820135865280605f830112156101ff576040519161020083016001600160401b038111848210176101eb576040528291610c408201918183116101ff57969796604001925b8284106104295750505050602084015260208401928352805f525f60205260405f2060018060a01b0333165f5260205260405f20825f5260205260405f2093518455825151600185015560025f9401935b835180518210156103f0576020015160108210156103dc578160051b015190610340818761061f565b6103c95782516007811015610161578154602085810151604087015169ffffffffffffffffffff1990931660ff9094169390931760089390931b68ffffffffffffffff00169290921790151560481b69ff000000000000000000161782556060840151600183810191909155608094909401518051600284015501516003919091015501610317565b634e487b7160e01b5f525f60045260245ffd5b634e487b7160e01b5f52603260045260245ffd5b7f5d14f8bf6f75758495bb0b0768b81cdebc7869d1f19edacc2f483ca0c89a1715606084866040519182523360208301526040820152a1005b83829998990360c081126101ff5761043f610600565b90853560078110156101ff57825260208601356001600160401b03811681036101ff57602083015260408601359081151582036101ff576040918284015260608701356060840152607f1901126101ff5760c09160209161049e6105e1565b6080880135815260a08801358482015260808201528152019301929796976102c6565b346101ff5760a03660031901126101ff576104da610588565b6104e261059e565b506084356001600160401b0381116101ff576020916105086105159236906004016105b4565b9160643590600435610632565b604051908152f35b346101ff5760203660031901126101ff576004359063ffffffff60e01b82168092036101ff576020916301ffc9a760e01b8114908115610577575b8115610566575b5015158152f35b6305c0089560e01b1490508361055f565b634c4e4f2360e11b81149150610558565b602435906001600160a01b03821682036101ff57565b604435906001600160a01b03821682036101ff57565b9181601f840112156101ff578235916001600160401b0383116101ff57602083818601950101116101ff57565b60405190604082018281106001600160401b038211176101eb57604052565b6040519060a082018281106001600160401b038211176101eb57604052565b9060108110156103dc5760021b01905f90565b5f8181526020818152604080832033845282528083206001600160a01b038616845290915290206001810154909590949093909285156106d757508554918282116106bf5750939460020193505f90505b8481106106935750505050505f90565b6106a883836106a2848861061f565b50610737565b156106b557600101610683565b5050505050600190565b638a8a722d60e01b5f5260045260245260445260645ffd5b8263369d19e760e11b5f526004523360245260018060a01b031660445260645ffd5b6001600160401b0316600401906001600160401b03821161071657565b634e487b7160e01b5f52601160045260245ffd5b9190820180921161071657565b90918154906001600160401b038260081c169060206001600160401b03610766610760856106f9565b946106f9565b1601916001600160401b038311610716576001600160401b038091169216908183116101ff5781116101ff5793810135930360208110610913575b5060ff8116600781101561016157801580610905575b156107c457505050505f90565b60018114806108f7575b156107db57505050505f90565b60028114806108e9575b156107f257505050505f90565b60038114806108dc575b1561080957505050505f90565b60048114806108cf575b1561082057505050505f90565b60058114806108c2575b1561083757505050505f90565b600614610886575b60481c60ff16610851575b5050600190565b60038101918254916002610865838561072a565b9101541061087f576108769161072a565b90555f8061084a565b5050505f90565b60018201548060801c84109081156108a6575b501561083f575050505f90565b6fffffffffffffffffffffffffffffffff91501683115f610899565b506001830154841461082a565b5060018301548411610813565b50600183015484106107fc565b5060018301548410156107e5565b5060018301548411156107ce565b5060018301548414156107b7565b5f939193199060200360031b1b16915f6107a156fea264697066735822122094fd93c79abf9581cd5bbbd218e91fd31fdb4d5d1118f2e34fe349965f8e0cdb64736f6c634300081c0033";
        public UniActionPolicyDeploymentBase() : base(BYTECODE) { }
        public UniActionPolicyDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ActionConfigsFunction : ActionConfigsFunctionBase { }

    [Function("actionConfigs", typeof(ActionConfigsOutputDTO))]
    public class ActionConfigsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "msgSender", 2)]
        public virtual string MsgSender { get; set; }
        [Parameter("address", "userOpSender", 3)]
        public virtual string UserOpSender { get; set; }
    }

    public partial class CheckActionFunction : CheckActionFunctionBase { }

    [Function("checkAction", "uint256")]
    public class CheckActionFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
        [Parameter("uint256", "value", 4)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class InitializeWithMultiplexerFunction : InitializeWithMultiplexerFunctionBase { }

    [Function("initializeWithMultiplexer")]
    public class InitializeWithMultiplexerFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "configId", 2)]
        public virtual byte[] ConfigId { get; set; }
        [Parameter("bytes", "initData", 3)]
        public virtual byte[] InitData { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class ActionConfigsOutputDTO : ActionConfigsOutputDTOBase { }

    [FunctionOutput]
    public class ActionConfigsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "valueLimitPerUse", 1)]
        public virtual BigInteger ValueLimitPerUse { get; set; }
        [Parameter("tuple", "paramRules", 2)]
        public virtual ParamRules ParamRules { get; set; }
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

    public partial class PolicyNotInitializedError : PolicyNotInitializedErrorBase { }

    [Error("PolicyNotInitialized")]
    public class PolicyNotInitializedErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "mxer", 2)]
        public virtual string Mxer { get; set; }
        [Parameter("address", "account", 3)]
        public virtual string Account { get; set; }
    }

    public partial class ValueLimitExceededError : ValueLimitExceededErrorBase { }

    [Error("ValueLimitExceeded")]
    public class ValueLimitExceededErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "limit", 3)]
        public virtual BigInteger Limit { get; set; }
    }
}
