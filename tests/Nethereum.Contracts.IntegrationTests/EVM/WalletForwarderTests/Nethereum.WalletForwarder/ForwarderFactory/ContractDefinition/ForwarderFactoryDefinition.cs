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

namespace Nethereum.WalletForwarder.Contracts.ForwarderFactory.ContractDefinition
{


    public partial class ForwarderFactoryDeployment : ForwarderFactoryDeploymentBase
    {
        public ForwarderFactoryDeployment() : base(BYTECODE) { }
        public ForwarderFactoryDeployment(string byteCode) : base(byteCode) { }
    }

    public class ForwarderFactoryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405234801561001057600080fd5b506105ab806100206000396000f3fe608060405234801561001057600080fd5b50600436106100415760003560e01c80636ac427111461004657806396909a79146100be578063bcebbcd414610176575b600080fd5b6100926004803603604081101561005c57600080fd5b81019080803573ffffffffffffffffffffffffffffffffffffffff1690602001909291908035906020019092919050505061024e565b604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390f35b610174600480360360208110156100d457600080fd5b81019080803590602001906401000000008111156100f157600080fd5b82018360208201111561010357600080fd5b8035906020019184602083028401116401000000008311171561012557600080fd5b919080806020026020016040519081016040528093929190818152602001838360200280828437600081840152601f19601f8201169050808301925050505050505091929192905050506103bb565b005b61024c6004803603604081101561018c57600080fd5b81019080803590602001906401000000008111156101a957600080fd5b8201836020820111156101bb57600080fd5b803590602001918460208302840111640100000000831117156101dd57600080fd5b919080806020026020016040519081016040528093929190818152602001838360200280828437600081840152601f19601f820116905080830192505050505050509192919290803573ffffffffffffffffffffffffffffffffffffffff169060200190929190505050610451565b005b60008061025b8484610509565b905060008490508192508273ffffffffffffffffffffffffffffffffffffffff166319ab453c8273ffffffffffffffffffffffffffffffffffffffff1663b269681d6040518163ffffffff1660e01b815260040160206040518083038186803b1580156102c757600080fd5b505afa1580156102db573d6000803e3d6000fd5b505050506040513d60208110156102f157600080fd5b81019080805190602001909291905050506040518263ffffffff1660e01b8152600401808273ffffffffffffffffffffffffffffffffffffffff168152602001915050600060405180830381600087803b15801561034e57600080fd5b505af1158015610362573d6000803e3d6000fd5b505050507f5dd8f89d9637eb98e980512c69ed8152bd1abced4c6785ceeb9f7628bd42e80982604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a1505092915050565b60005b815181101561044d5760008282815181106103d557fe5b602002602001015190508073ffffffffffffffffffffffffffffffffffffffff16636b9f96ea6040518163ffffffff1660e01b8152600401600060405180830381600087803b15801561042757600080fd5b505af115801561043b573d6000803e3d6000fd5b505050505080806001019150506103be565b5050565b60005b825181101561050457600083828151811061046b57fe5b602002602001015190508073ffffffffffffffffffffffffffffffffffffffff16633ef13367846040518263ffffffff1660e01b8152600401808273ffffffffffffffffffffffffffffffffffffffff168152602001915050600060405180830381600087803b1580156104de57600080fd5b505af11580156104f2573d6000803e3d6000fd5b50505050508080600101915050610454565b505050565b6000808360601b90506040517f3d602d80600a3d3981f3363d3d373d3d3d363d7300000000000000000000000081528160148201527f5af43d82803e903d91602b57fd5bf300000000000000000000000000000000006028820152836037826000f5925050509291505056fea26469706673582212201efa05a68f5090d70fe39099a42f52800e26b5c5c3d508cd58ffee86a92c3ed264736f6c63430007060033";
        public ForwarderFactoryDeploymentBase() : base(BYTECODE) { }
        public ForwarderFactoryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CloneForwarderFunction : CloneForwarderFunctionBase { }

    [Function("cloneForwarder", "address")]
    public class CloneForwarderFunctionBase : FunctionMessage
    {
        [Parameter("address", "forwarder", 1)]
        public virtual string Forwarder { get; set; }
        [Parameter("uint256", "salt", 2)]
        public virtual BigInteger Salt { get; set; }
    }

    public partial class FlushEtherFunction : FlushEtherFunctionBase { }

    [Function("flushEther")]
    public class FlushEtherFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "forwarders", 1)]
        public virtual List<string> Forwarders { get; set; }
    }

    public partial class FlushTokensFunction : FlushTokensFunctionBase { }

    [Function("flushTokens")]
    public class FlushTokensFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "forwarders", 1)]
        public virtual List<string> Forwarders { get; set; }
        [Parameter("address", "tokenAddres", 2)]
        public virtual string TokenAddres { get; set; }
    }

    public partial class ForwarderClonedEventDTO : ForwarderClonedEventDTOBase { }

    [Event("ForwarderCloned")]
    public class ForwarderClonedEventDTOBase : IEventDTO
    {
        [Parameter("address", "clonedAdress", 1, false )]
        public virtual string ClonedAdress { get; set; }
    }






}
