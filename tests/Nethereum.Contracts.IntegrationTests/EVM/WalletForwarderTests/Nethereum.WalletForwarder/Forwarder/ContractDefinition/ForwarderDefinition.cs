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

namespace Nethereum.WalletForwarder.Contracts.Forwarder.ContractDefinition
{


    public partial class ForwarderDeployment : ForwarderDeploymentBase
    {
        public ForwarderDeployment() : base(BYTECODE) { }
        public ForwarderDeployment(string byteCode) : base(byteCode) { }
    }

    public class ForwarderDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405260008060146101000a81548160ff02191690831515021790555034801561002a57600080fd5b50336000806101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055506001600060146101000a81548160ff021916908315150217905550610822806100956000396000f3fe6080604052600436106100595760003560e01c806319ab453c146101565780633ccfd60b146101a75780633ef13367146101b15780635e949fa0146102025780636b9f96ea14610253578063b269681d1461025d57610151565b366101515760008054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff166108fc349081150290604051600060405180830381858888f193505050501580156100c4573d6000803e3d6000fd5b507f69b31548dea9b3b707b4dff357d326e3e9348b24e7a6080a218a6edeeec48f9b3334600036604051808573ffffffffffffffffffffffffffffffffffffffff168152602001848152602001806020018281038252848482818152602001925080828437600081840152601f19601f8201169050808301925050509550505050505060405180910390a1005b600080fd5b34801561016257600080fd5b506101a56004803603602081101561017957600080fd5b81019080803573ffffffffffffffffffffffffffffffffffffffff16906020019092919050505061029e565b005b6101af610311565b005b3480156101bd57600080fd5b50610200600480360360208110156101d457600080fd5b81019080803573ffffffffffffffffffffffffffffffffffffffff169060200190929190505050610438565b005b34801561020e57600080fd5b506102516004803603602081101561022557600080fd5b81019080803573ffffffffffffffffffffffffffffffffffffffff16906020019092919050505061063e565b005b61025b610742565b005b34801561026957600080fd5b506102726107c8565b604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390f35b600060149054906101000a900460ff1661030e57806000806101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055506001600060146101000a81548160ff0219169083151502179055505b50565b60008054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff16146103d2576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004018080602001828103825260108152602001807f4f6e6c792064657374696e6174696f6e0000000000000000000000000000000081525060200191505060405180910390fd5b60003090503373ffffffffffffffffffffffffffffffffffffffff166108fc8273ffffffffffffffffffffffffffffffffffffffff16319081150290604051600060405180830381858888f19350505050158015610434573d6000803e3d6000fd5b5050565b600081905060008173ffffffffffffffffffffffffffffffffffffffff166370a08231306040518263ffffffff1660e01b8152600401808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060206040518083038186803b1580156104a657600080fd5b505afa1580156104ba573d6000803e3d6000fd5b505050506040513d60208110156104d057600080fd5b8101908080519060200190929190505050905060008114156104f157600080fd5b8173ffffffffffffffffffffffffffffffffffffffff1663a9059cbb60008054906101000a900473ffffffffffffffffffffffffffffffffffffffff16836040518363ffffffff1660e01b8152600401808373ffffffffffffffffffffffffffffffffffffffff16815260200182815260200192505050602060405180830381600087803b15801561058257600080fd5b505af1158015610596573d6000803e3d6000fd5b505050506040513d60208110156105ac57600080fd5b81019080805190602001909291905050506105c657600080fd5b7fb4bdccee2343c0b5e592d459c20eb1fa451c96bf88fb685a11aecda6b4ec76b1308285604051808473ffffffffffffffffffffffffffffffffffffffff1681526020018381526020018273ffffffffffffffffffffffffffffffffffffffff168152602001935050505060405180910390a1505050565b60008054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff16146106ff576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004018080602001828103825260108152602001807f4f6e6c792064657374696e6174696f6e0000000000000000000000000000000081525060200191505060405180910390fd5b806000806101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff16021790555050565b600030905060008054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff166108fc8273ffffffffffffffffffffffffffffffffffffffff16319081150290604051600060405180830381858888f193505050501580156107c4573d6000803e3d6000fd5b5050565b60008054906101000a900473ffffffffffffffffffffffffffffffffffffffff168156fea2646970667358221220cb5789c55684de2d7059524ffe8adc5e3cf2d2553794f6747c593b596355cdb864736f6c63430007060033";
        public ForwarderDeploymentBase() : base(BYTECODE) { }
        public ForwarderDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ChangeDestinationFunction : ChangeDestinationFunctionBase { }

    [Function("changeDestination")]
    public class ChangeDestinationFunctionBase : FunctionMessage
    {
        [Parameter("address", "newDestination", 1)]
        public virtual string NewDestination { get; set; }
    }

    public partial class DestinationFunction : DestinationFunctionBase { }

    [Function("destination", "address")]
    public class DestinationFunctionBase : FunctionMessage
    {

    }

    public partial class FlushFunction : FlushFunctionBase { }

    [Function("flush")]
    public class FlushFunctionBase : FunctionMessage
    {

    }

    public partial class FlushTokensFunction : FlushTokensFunctionBase { }

    [Function("flushTokens")]
    public class FlushTokensFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenContractAddress", 1)]
        public virtual string TokenContractAddress { get; set; }
    }

    public partial class InitFunction : InitFunctionBase { }

    [Function("init")]
    public class InitFunctionBase : FunctionMessage
    {
        [Parameter("address", "newDestination", 1)]
        public virtual string NewDestination { get; set; }
    }

    public partial class WithdrawFunction : WithdrawFunctionBase { }

    [Function("withdraw")]
    public class WithdrawFunctionBase : FunctionMessage
    {

    }

    public partial class ForwarderDepositedEventDTO : ForwarderDepositedEventDTOBase { }

    [Event("ForwarderDeposited")]
    public class ForwarderDepositedEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, false )]
        public virtual string From { get; set; }
        [Parameter("uint256", "value", 2, false )]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3, false )]
        public virtual byte[] Data { get; set; }
    }

    public partial class TokensFlushedEventDTO : TokensFlushedEventDTOBase { }

    [Event("TokensFlushed")]
    public class TokensFlushedEventDTOBase : IEventDTO
    {
        [Parameter("address", "forwarderAddress", 1, false )]
        public virtual string ForwarderAddress { get; set; }
        [Parameter("uint256", "value", 2, false )]
        public virtual BigInteger Value { get; set; }
        [Parameter("address", "tokenContractAddress", 3, false )]
        public virtual string TokenContractAddress { get; set; }
    }



    public partial class DestinationOutputDTO : DestinationOutputDTOBase { }

    [FunctionOutput]
    public class DestinationOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }








}
