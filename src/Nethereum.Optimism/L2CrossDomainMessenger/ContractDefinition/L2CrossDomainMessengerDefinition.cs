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

namespace Nethereum.Optimism.L2CrossDomainMessenger.ContractDefinition
{


    public partial class L2CrossDomainMessengerDeployment : L2CrossDomainMessengerDeploymentBase
    {
        public L2CrossDomainMessengerDeployment() : base(BYTECODE) { }
        public L2CrossDomainMessengerDeployment(string byteCode) : base(byteCode) { }
    }

    public class L2CrossDomainMessengerDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "6080604052600480546001600160a01b03191661dead17905534801561002457600080fd5b506040516109dc3803806109dc83398101604081905261004391610068565b600580546001600160a01b0319166001600160a01b0392909216919091179055610098565b60006020828403121561007a57600080fd5b81516001600160a01b038116811461009157600080fd5b9392505050565b610935806100a76000396000f3fe608060405234801561001057600080fd5b50600436106100885760003560e01c8063a71198691161005b578063a71198691461011d578063b1b1b20914610130578063cbd4ece914610153578063ecc704281461016657600080fd5b806321d800ec1461008d5780633dbb202b146100c55780636e296e45146100da57806382e3702d146100fa575b600080fd5b6100b061009b3660046105e5565b60006020819052908152604090205460ff1681565b60405190151581526020015b60405180910390f35b6100d86100d33660046106bd565b61017d565b005b6100e2610274565b6040516001600160a01b0390911681526020016100bc565b6100b06101083660046105e5565b60026020526000908152604090205460ff1681565b6005546100e2906001600160a01b031681565b6100b061013e3660046105e5565b60016020526000908152604090205460ff1681565b6100d8610161366004610728565b6102e9565b61016f60035481565b6040519081526020016100bc565b600061018d843385600354610598565b805160208083019190912060009081526002909152604090819020805460ff19166001179055516332bea07760e21b8152909150602160991b9063cafa81dc906101db9084906004016107e6565b600060405180830381600087803b1580156101f557600080fd5b505af1158015610209573d6000803e3d6000fd5b50505050836001600160a01b03167fcb0f7ffd78f9aee47a248fae8db181db6eee833039123e026dcbff529522e52a33856003548660405161024e9493929190610800565b60405180910390a26001600360008282546102699190610841565b909155505050505050565b6004546000906001600160a01b031661dead14156102d95760405162461bcd60e51b815260206004820152601f60248201527f78446f6d61696e4d65737361676553656e646572206973206e6f74207365740060448201526064015b60405180910390fd5b506004546001600160a01b031690565b6005546001600160a01b03167311110000000000000000000000000000000011101933016001600160a01b0316146103735760405162461bcd60e51b815260206004820152602760248201527f50726f7669646564206d65737361676520636f756c64206e6f742062652076656044820152663934b334b2b21760c91b60648201526084016102d0565b600061038185858585610598565b8051602080830191909120600081815260019092526040909120549192509060ff16156104045760405162461bcd60e51b815260206004820152602b60248201527f50726f7669646564206d6573736167652068617320616c72656164792062656560448201526a37103932b1b2b4bb32b21760a91b60648201526084016102d0565b6001600160a01b038616602160991b141561043b576000908152600160208190526040909120805460ff1916909117905550610592565b600480546001600160a01b0319166001600160a01b038781169190911790915560405160009188169061046f908790610867565b6000604051808303816000865af19150503d80600081146104ac576040519150601f19603f3d011682016040523d82523d6000602084013e6104b1565b606091505b5050600480546001600160a01b03191661dead17905590508015156001141561051c576000828152600160208190526040808320805460ff19169092179091555183917f4641df4a962071e12719d8c8c8e5ac7fc4d97b927346a3d7a335b1f7517e133c91a2610548565b60405182907f99d0e048484baa1b1540b1367cb128acd7ab2946d1ed91ec10e3c85e4bf51b8f90600090a25b600083334360405160200161055f93929190610883565b60408051601f1981840301815291815281516020928301206000908152918290529020805460ff19166001179055505050505b50505050565b6060848484846040516024016105b194939291906108c2565b60408051601f198184030181529190526020810180516001600160e01b031663cbd4ece960e01b1790529050949350505050565b6000602082840312156105f757600080fd5b5035919050565b80356001600160a01b038116811461061557600080fd5b919050565b634e487b7160e01b600052604160045260246000fd5b600082601f83011261064157600080fd5b813567ffffffffffffffff8082111561065c5761065c61061a565b604051601f8301601f19908116603f011681019082821181831017156106845761068461061a565b8160405283815286602085880101111561069d57600080fd5b836020870160208301376000602085830101528094505050505092915050565b6000806000606084860312156106d257600080fd5b6106db846105fe565b9250602084013567ffffffffffffffff8111156106f757600080fd5b61070386828701610630565b925050604084013563ffffffff8116811461071d57600080fd5b809150509250925092565b6000806000806080858703121561073e57600080fd5b610747856105fe565b9350610755602086016105fe565b9250604085013567ffffffffffffffff81111561077157600080fd5b61077d87828801610630565b949793965093946060013593505050565b60005b838110156107a9578181015183820152602001610791565b838111156105925750506000910152565b600081518084526107d281602086016020860161078e565b601f01601f19169290920160200192915050565b6020815260006107f960208301846107ba565b9392505050565b6001600160a01b0385168152608060208201819052600090610824908301866107ba565b905083604083015263ffffffff8316606083015295945050505050565b6000821982111561086257634e487b7160e01b600052601160045260246000fd5b500190565b6000825161087981846020870161078e565b9190910192915050565b6000845161089581846020890161078e565b60609490941b6bffffffffffffffffffffffff191691909301908152601481019190915260340192915050565b6001600160a01b038581168252841660208201526080604082018190526000906108ee908301856107ba565b90508260608301529594505050505056fea2646970667358221220194cc75b6343a58adfe56cad868eefe14784917558bf2c5d7a3f188ded04bdf264736f6c634300080b0033";
        public L2CrossDomainMessengerDeploymentBase() : base(BYTECODE) { }
        public L2CrossDomainMessengerDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_l1CrossDomainMessenger", 1)]
        public virtual string L1CrossDomainMessenger { get; set; }
    }

    public partial class L1CrossDomainMessengerFunction : L1CrossDomainMessengerFunctionBase { }

    [Function("l1CrossDomainMessenger", "address")]
    public class L1CrossDomainMessengerFunctionBase : FunctionMessage
    {

    }

    public partial class MessageNonceFunction : MessageNonceFunctionBase { }

    [Function("messageNonce", "uint256")]
    public class MessageNonceFunctionBase : FunctionMessage
    {

    }

    public partial class RelayMessageFunction : RelayMessageFunctionBase { }

    [Function("relayMessage")]
    public class RelayMessageFunctionBase : FunctionMessage
    {
        [Parameter("address", "_target", 1)]
        public virtual string Target { get; set; }
        [Parameter("address", "_sender", 2)]
        public virtual string Sender { get; set; }
        [Parameter("bytes", "_message", 3)]
        public virtual byte[] Message { get; set; }
        [Parameter("uint256", "_messageNonce", 4)]
        public virtual BigInteger MessageNonce { get; set; }
    }

    public partial class RelayedMessagesFunction : RelayedMessagesFunctionBase { }

    [Function("relayedMessages", "bool")]
    public class RelayedMessagesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SendMessageFunction : SendMessageFunctionBase { }

    [Function("sendMessage")]
    public class SendMessageFunctionBase : FunctionMessage
    {
        [Parameter("address", "_target", 1)]
        public virtual string Target { get; set; }
        [Parameter("bytes", "_message", 2)]
        public virtual byte[] Message { get; set; }
        [Parameter("uint32", "_gasLimit", 3)]
        public virtual uint GasLimit { get; set; }
    }

    public partial class SentMessagesFunction : SentMessagesFunctionBase { }

    [Function("sentMessages", "bool")]
    public class SentMessagesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SuccessfulMessagesFunction : SuccessfulMessagesFunctionBase { }

    [Function("successfulMessages", "bool")]
    public class SuccessfulMessagesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class XDomainMessageSenderFunction : XDomainMessageSenderFunctionBase { }

    [Function("xDomainMessageSender", "address")]
    public class XDomainMessageSenderFunctionBase : FunctionMessage
    {

    }

    public partial class FailedRelayedMessageEventDTO : FailedRelayedMessageEventDTOBase { }

    [Event("FailedRelayedMessage")]
    public class FailedRelayedMessageEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "msgHash", 1, true)]
        public virtual byte[] MsgHash { get; set; }
    }

    public partial class RelayedMessageEventDTO : RelayedMessageEventDTOBase { }

    [Event("RelayedMessage")]
    public class RelayedMessageEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "msgHash", 1, true)]
        public virtual byte[] MsgHash { get; set; }
    }

    public partial class SentMessageEventDTO : SentMessageEventDTOBase { }

    [Event("SentMessage")]
    public class SentMessageEventDTOBase : IEventDTO
    {
        [Parameter("address", "target", 1, true)]
        public virtual string Target { get; set; }
        [Parameter("address", "sender", 2, false)]
        public virtual string Sender { get; set; }
        [Parameter("bytes", "message", 3, false)]
        public virtual byte[] Message { get; set; }
        [Parameter("uint256", "messageNonce", 4, false)]
        public virtual BigInteger MessageNonce { get; set; }
        [Parameter("uint256", "gasLimit", 5, false)]
        public virtual BigInteger GasLimit { get; set; }
    }

    public partial class L1CrossDomainMessengerOutputDTO : L1CrossDomainMessengerOutputDTOBase { }

    [FunctionOutput]
    public class L1CrossDomainMessengerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class MessageNonceOutputDTO : MessageNonceOutputDTOBase { }

    [FunctionOutput]
    public class MessageNonceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class RelayedMessagesOutputDTO : RelayedMessagesOutputDTOBase { }

    [FunctionOutput]
    public class RelayedMessagesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class SentMessagesOutputDTO : SentMessagesOutputDTOBase { }

    [FunctionOutput]
    public class SentMessagesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class SuccessfulMessagesOutputDTO : SuccessfulMessagesOutputDTOBase { }

    [FunctionOutput]
    public class SuccessfulMessagesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class XDomainMessageSenderOutputDTO : XDomainMessageSenderOutputDTOBase { }

    [FunctionOutput]
    public class XDomainMessageSenderOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
