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

namespace Nethereum.Optimism.OVM_L2ToL1MessagePasser.ContractDefinition
{


    public partial class OVM_L2ToL1MessagePasserDeployment : OVM_L2ToL1MessagePasserDeploymentBase
    {
        public OVM_L2ToL1MessagePasserDeployment() : base(BYTECODE) { }
        public OVM_L2ToL1MessagePasserDeployment(string byteCode) : base(byteCode) { }
    }

    public class OVM_L2ToL1MessagePasserDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405234801561001057600080fd5b50610242806100206000396000f3fe608060405234801561001057600080fd5b50600436106100365760003560e01c806382e3702d1461003b578063cafa81dc14610072575b600080fd5b61005e6100493660046100d6565b60006020819052908152604090205460ff1681565b604051901515815260200160405180910390f35b610085610080366004610105565b610087565b005b6001600080833360405160200161009f9291906101b6565b60408051808303601f19018152918152815160209283012083529082019290925201600020805460ff191691151591909117905550565b6000602082840312156100e857600080fd5b5035919050565b634e487b7160e01b600052604160045260246000fd5b60006020828403121561011757600080fd5b813567ffffffffffffffff8082111561012f57600080fd5b818401915084601f83011261014357600080fd5b813581811115610155576101556100ef565b604051601f8201601f19908116603f0116810190838211818310171561017d5761017d6100ef565b8160405282815287602084870101111561019657600080fd5b826020860160208301376000928101602001929092525095945050505050565b6000835160005b818110156101d757602081870181015185830152016101bd565b818111156101e6576000828501525b5060609390931b6bffffffffffffffffffffffff1916919092019081526014019291505056fea264697066735822122017c296a1b390caeafb6a60b0092ce6b0ba6702fcec54f3a1e267ff62e433e41964736f6c634300080b0033";
        public OVM_L2ToL1MessagePasserDeploymentBase() : base(BYTECODE) { }
        public OVM_L2ToL1MessagePasserDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class PassMessageToL1Function : PassMessageToL1FunctionBase { }

    [Function("passMessageToL1")]
    public class PassMessageToL1FunctionBase : FunctionMessage
    {
        [Parameter("bytes", "_message", 1)]
        public virtual byte[] Message { get; set; }
    }

    public partial class SentMessagesFunction : SentMessagesFunctionBase { }

    [Function("sentMessages", "bool")]
    public class SentMessagesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class L2ToL1MessageEventDTO : L2ToL1MessageEventDTOBase { }

    [Event("L2ToL1Message")]
    public class L2ToL1MessageEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "_nonce", 1, false)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("address", "_sender", 2, false)]
        public virtual string Sender { get; set; }
        [Parameter("bytes", "_data", 3, false)]
        public virtual byte[] Data { get; set; }
    }



    public partial class SentMessagesOutputDTO : SentMessagesOutputDTOBase { }

    [FunctionOutput]
    public class SentMessagesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
