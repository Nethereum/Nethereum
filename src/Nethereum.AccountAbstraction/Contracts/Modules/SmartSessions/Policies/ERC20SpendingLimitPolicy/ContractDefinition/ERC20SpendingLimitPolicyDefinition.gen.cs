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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy.ContractDefinition
{


    public partial class ERC20SpendingLimitPolicyDeployment : ERC20SpendingLimitPolicyDeploymentBase
    {
        public ERC20SpendingLimitPolicyDeployment() : base(BYTECODE) { }
        public ERC20SpendingLimitPolicyDeployment(string byteCode) : base(byteCode) { }
    }

    public class ERC20SpendingLimitPolicyDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080806040523460155761096b908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806301ffc9a71461004457806305c008951461003f5763989c9e461461003a575f80fd5b61015a565b6100f3565b346100b05760203660031901126100b05760043563ffffffff60e01b81168091036100b0576301ffc9a760e01b811490811561009f575b811561008e575b50151560805260206080f35b6305c0089560e01b14905081610082565b634c4e4f2360e11b8114915061007b565b5f80fd5b6001600160a01b038116036100b057565b9181601f840112156100b05782359167ffffffffffffffff83116100b057602083818601950101116100b057565b346100b05760a03660031901126100b057602435600435610113826100b4565b60443591610120836100b4565b6084359160643567ffffffffffffffff84116100b05760209461014a6101529536906004016100c5565b949093610351565b604051908152f35b346100b05760603660031901126100b057600435610177816100b4565b6024359060443567ffffffffffffffff81116100b05761019e6101a69136906004016100c5565b8101906104c6565b90916101ce6101bc855f525f60205260405f2090565b335f9081526020919091526040902090565b916101e28284905f5260205260405f205490565b806102e4575b505f5b845181101561029e5761020e6102018287610563565b516001600160a01b031690565b6102188284610563565b516001600160a01b0382161561028257801561027057610262610256836102699360016102488198978b8f610681565b01556001600160a01b031690565b6001600160a01b031690565b858761073e565b50016101eb565b63337fcce960e21b5f5260045260245ffd5b637330680360e01b5f526001600160a01b03821660045260245ffd5b604080518781523360208201526001600160a01b038516918101919091527f5d14f8bf6f75758495bb0b0768b81cdebc7869d1f19edacc2f483ca0c89a171590606090a1005b5f5b8181106102ff5750506102f982846106e0565b5f6101e8565b805f61031e6103176102566102566001968a8c6108a5565b878b610681565b818482015555016102e6565b634e487b7160e01b5f52601160045260245ffd5b5f1981019190821161034c57565b61032a565b9294909493919361040d57610365916105ee565b93901561040457610377838284610681565b60018101549080549086820180921161034c578282111561039e5750505050505050600190565b819055810390811161034c57604080519384523360208501526001600160a01b039485169084015292166060820152608081019290925260a08201527f8c8443cbf8877c7ddfe6d58dd9d439df5da8eb966f6aee4edb64cdb36f6b9c659060c090a15f90565b50505050600190565b5050505050600190565b634e487b7160e01b5f52604160045260245ffd5b6040519190601f01601f1916820167ffffffffffffffff81118382101761045157604052565b610417565b67ffffffffffffffff81116104515760051b60200190565b9080601f830112156100b057813561048d61048882610456565b61042b565b9260208085848152019260051b8201019283116100b057602001905b8282106104b65750505090565b81358152602091820191016104a9565b9190916040818403126100b057803567ffffffffffffffff81116100b057810183601f820112156100b05780359061050061048883610456565b9160208084838152019160051b830101918683116100b057602001905b8282106105495750505092602082013567ffffffffffffffff81116100b057610546920161046e565b90565b602080918335610558816100b4565b81520191019061051d565b80518210156105775760209160051b010190565b634e487b7160e01b5f52603260045260245ffd5b90929192836004116100b05783116100b057600401916003190190565b908160609103126100b05780356105be816100b4565b91604060208301356105cf816100b4565b92013590565b91908260409103126100b057602082356105cf816100b4565b816004116100b05780356001600160e01b03191663095ea7b360e01b810361062f5750816106279261061f9261058b565b8101906105d5565b600192909150565b63a9059cbb60e01b810361064c5750816106279261061f9261058b565b6323b872dd60e01b146106615750505f905f90565b816106779261066f9261058b565b8101906105a8565b9291505060019190565b9091906001600160a01b03821680156106ce57505f908152600160209081526040808320338452909152902061054692916106b99182565b9060018060a01b03165f5260205260405f2090565b637330680360e01b5f5260045260245ffd5b6106f38282905f5260205260405f205490565b9060015b828111156107055750505050565b80830383811161034c5761072661072d91865f528460205260405f206108b4565b85846107d7565b505f19811461034c576001016106f7565b916001830192815f52836020526107688360405f209060018060a01b03165f5260205260405f2090565b546107cf57825f528060205260405f208054607f81116107bf57846106b9938360018488826107ab976107b99c9b99010155019055905f5260205260405f205490565b94905f5260205260405f2090565b55600190565b638277484f5f526020526024601cfd5b505050505f90565b906001820191835f52826020526108018260405f209060018060a01b03165f5260205260405f2090565b5493841561089c575f1985019385851161034c57610849846106b9945f986107b998838b52826020528361083860408d205461033e565b808303610856575b505050506108f0565b905f5260205260405f2090565b6108929261088461087b6106b993610876868a905f5260205260405f2090565b6108b4565b809285896108d6565b88905f5260205260405f2090565b555f808381610840565b50505050505f90565b61054692915f5260205260405f205b80548210156108c557016001015490565b50638277484f5f526020526024601cfd5b905f5260205260405f2080548210156108c5570160010155565b905f5260205260405f208054908115610931575f1982019180831161034c578154831015610920575f9082015555565b82638277484f5f526020526024601cfd5b505056fea2646970667358221220fccd4f92f71163690d6e64a83ebbbb11bc623f1e64a88fb6e05d7362181c706964736f6c634300081c0033";
        public ERC20SpendingLimitPolicyDeploymentBase() : base(BYTECODE) { }
        public ERC20SpendingLimitPolicyDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CheckActionFunction : CheckActionFunctionBase { }

    [Function("checkAction", "uint256")]
    public class CheckActionFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
        [Parameter("address", "target", 3)]
        public virtual string Target { get; set; }
        [Parameter("uint256", "value", 4)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "callData", 5)]
        public virtual byte[] CallData { get; set; }
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

    public partial class TokenSpentEventDTO : TokenSpentEventDTOBase { }

    [Event("TokenSpent")]
    public class TokenSpentEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, false )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "multiplexer", 2, false )]
        public virtual string Multiplexer { get; set; }
        [Parameter("address", "token", 3, false )]
        public virtual string Token { get; set; }
        [Parameter("address", "account", 4, false )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "amount", 5, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "remaining", 6, false )]
        public virtual BigInteger Remaining { get; set; }
    }

    public partial class InvalidLimitError : InvalidLimitErrorBase { }

    [Error("InvalidLimit")]
    public class InvalidLimitErrorBase : IErrorDTO
    {
        [Parameter("uint256", "limit", 1)]
        public virtual BigInteger Limit { get; set; }
    }

    public partial class InvalidTokenAddressError : InvalidTokenAddressErrorBase { }

    [Error("InvalidTokenAddress")]
    public class InvalidTokenAddressErrorBase : IErrorDTO
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
    }
}
