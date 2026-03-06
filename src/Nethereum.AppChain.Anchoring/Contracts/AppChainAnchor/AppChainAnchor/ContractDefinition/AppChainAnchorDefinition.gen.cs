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
using Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor.ContractDefinition
{


    public partial class AppChainAnchorDeployment : AppChainAnchorDeploymentBase
    {
        public AppChainAnchorDeployment() : base(BYTECODE) { }
        public AppChainAnchorDeployment(string byteCode) : base(byteCode) { }
    }

    public class AppChainAnchorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a060405234801561001057600080fd5b5060405161055538038061055583398101604081905261002f91610059565b608091909152600080546001600160a01b0319166001600160a01b03909216919091179055610096565b6000806040838503121561006c57600080fd5b825160208401519092506001600160a01b038116811461008b57600080fd5b809150509250929050565b6080516104a46100b1600039600061019e01526104a46000f3fe608060405234801561001057600080fd5b50600436106100885760003560e01c80634c7df18f1161005b5780634c7df18f146101375780635c1bba381461016e5780638347092314610199578063fa2c2380146101c057600080fd5b806307e2da961461008d578063231b5304146100a95780632547fa3e146100cc578063368b733e146100e1575b600080fd5b61009660025481565b6040519081526020015b60405180910390f35b6100bc6100b73660046103f3565b6101d3565b60405190151581526020016100a0565b6100df6100da366004610425565b610224565b005b6101176100ef366004610455565b6001602081905260009182526040909120805491810154600282015460039092015490919084565b6040805194855260208501939093529183015260608201526080016100a0565b610117610145366004610455565b600090815260016020819052604090912080549181015460028201546003909201549293909290565b600054610181906001600160a01b031681565b6040516001600160a01b0390911681526020016100a0565b6100967f000000000000000000000000000000000000000000000000000000000000000081565b6100df6101ce3660046103f3565b6102c4565b6000848152600160205260408120600381015482036101f657600091505061021c565b8054851480156102095750838160010154145b80156102185750828160020154145b9150505b949350505050565b6000546001600160a01b031633146102745760405162461bcd60e51b815260206004820152600e60248201526d27b7363c9039b2b8bab2b731b2b960911b60448201526064015b60405180910390fd5b600080546001600160a01b038381166001600160a01b0319831681178455604051919092169283917f6ec88bae255aa7e73521c3beb17e9bc7940169e669440c5531733c0d2e91110d9190a35050565b6000546001600160a01b0316331461030f5760405162461bcd60e51b815260206004820152600e60248201526d27b7363c9039b2b8bab2b731b2b960911b604482015260640161026b565b60025484116103565760405162461bcd60e51b8152602060048201526013602482015272213637b1b59036bab9ba103132903732bbb2b960691b604482015260640161026b565b60408051608081018252848152602080820185815282840185815242606080860191825260008b8152600180875290889020965187559351938601939093559051600280860191909155905160039094019390935591879055825186815290810185905291820183905285917f981018a420acb021132dd29cbb71fb8b86dc765b170c0d8ec191c9f1ea810a07910160405180910390a250505050565b6000806000806080858703121561040957600080fd5b5050823594602084013594506040840135936060013592509050565b60006020828403121561043757600080fd5b81356001600160a01b038116811461044e57600080fd5b9392505050565b60006020828403121561046757600080fd5b503591905056fea26469706673582212205e37cff56522440ff365e5a185ec5139dade5ae7746b5f9b61a5a81997ebe0b264736f6c63430008130033";
        public AppChainAnchorDeploymentBase() : base(BYTECODE) { }
        public AppChainAnchorDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("uint256", "_appChainId", 1)]
        public virtual BigInteger AppChainId { get; set; }
        [Parameter("address", "_sequencer", 2)]
        public virtual string Sequencer { get; set; }
    }

    public partial class AnchorFunction : AnchorFunctionBase { }

    [Function("anchor")]
    public class AnchorFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "blockNumber", 1)]
        public virtual BigInteger BlockNumber { get; set; }
        [Parameter("bytes32", "stateRoot", 2)]
        public virtual byte[] StateRoot { get; set; }
        [Parameter("bytes32", "txRoot", 3)]
        public virtual byte[] TxRoot { get; set; }
        [Parameter("bytes32", "receiptRoot", 4)]
        public virtual byte[] ReceiptRoot { get; set; }
    }

    public partial class AnchorsFunction : AnchorsFunctionBase { }

    [Function("anchors", typeof(AnchorsOutputDTO))]
    public class AnchorsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AppChainIdFunction : AppChainIdFunctionBase { }

    [Function("appChainId", "uint256")]
    public class AppChainIdFunctionBase : FunctionMessage
    {

    }

    public partial class GetAnchorFunction : GetAnchorFunctionBase { }

    [Function("getAnchor", typeof(GetAnchorOutputDTO))]
    public class GetAnchorFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "blockNumber", 1)]
        public virtual BigInteger BlockNumber { get; set; }
    }

    public partial class LatestBlockFunction : LatestBlockFunctionBase { }

    [Function("latestBlock", "uint256")]
    public class LatestBlockFunctionBase : FunctionMessage
    {

    }

    public partial class SequencerFunction : SequencerFunctionBase { }

    [Function("sequencer", "address")]
    public class SequencerFunctionBase : FunctionMessage
    {

    }

    public partial class SetSequencerFunction : SetSequencerFunctionBase { }

    [Function("setSequencer")]
    public class SetSequencerFunctionBase : FunctionMessage
    {
        [Parameter("address", "newSequencer", 1)]
        public virtual string NewSequencer { get; set; }
    }

    public partial class VerifyAnchorFunction : VerifyAnchorFunctionBase { }

    [Function("verifyAnchor", "bool")]
    public class VerifyAnchorFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "blockNumber", 1)]
        public virtual BigInteger BlockNumber { get; set; }
        [Parameter("bytes32", "stateRoot", 2)]
        public virtual byte[] StateRoot { get; set; }
        [Parameter("bytes32", "txRoot", 3)]
        public virtual byte[] TxRoot { get; set; }
        [Parameter("bytes32", "receiptRoot", 4)]
        public virtual byte[] ReceiptRoot { get; set; }
    }



    public partial class AnchorsOutputDTO : AnchorsOutputDTOBase { }

    [FunctionOutput]
    public class AnchorsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "stateRoot", 1)]
        public virtual byte[] StateRoot { get; set; }
        [Parameter("bytes32", "txRoot", 2)]
        public virtual byte[] TxRoot { get; set; }
        [Parameter("bytes32", "receiptRoot", 3)]
        public virtual byte[] ReceiptRoot { get; set; }
        [Parameter("uint256", "timestamp", 4)]
        public virtual BigInteger Timestamp { get; set; }
    }

    public partial class AppChainIdOutputDTO : AppChainIdOutputDTOBase { }

    [FunctionOutput]
    public class AppChainIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetAnchorOutputDTO : GetAnchorOutputDTOBase { }

    [FunctionOutput]
    public class GetAnchorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "stateRoot", 1)]
        public virtual byte[] StateRoot { get; set; }
        [Parameter("bytes32", "txRoot", 2)]
        public virtual byte[] TxRoot { get; set; }
        [Parameter("bytes32", "receiptRoot", 3)]
        public virtual byte[] ReceiptRoot { get; set; }
        [Parameter("uint256", "timestamp", 4)]
        public virtual BigInteger Timestamp { get; set; }
    }

    public partial class LatestBlockOutputDTO : LatestBlockOutputDTOBase { }

    [FunctionOutput]
    public class LatestBlockOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class SequencerOutputDTO : SequencerOutputDTOBase { }

    [FunctionOutput]
    public class SequencerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class VerifyAnchorOutputDTO : VerifyAnchorOutputDTOBase { }

    [FunctionOutput]
    public class VerifyAnchorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class AnchoredEventDTO : AnchoredEventDTOBase { }

    [Event("Anchored")]
    public class AnchoredEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "blockNumber", 1, true )]
        public virtual BigInteger BlockNumber { get; set; }
        [Parameter("bytes32", "stateRoot", 2, false )]
        public virtual byte[] StateRoot { get; set; }
        [Parameter("bytes32", "txRoot", 3, false )]
        public virtual byte[] TxRoot { get; set; }
        [Parameter("bytes32", "receiptRoot", 4, false )]
        public virtual byte[] ReceiptRoot { get; set; }
    }

    public partial class SequencerChangedEventDTO : SequencerChangedEventDTOBase { }

    [Event("SequencerChanged")]
    public class SequencerChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldSequencer", 1, true )]
        public virtual string OldSequencer { get; set; }
        [Parameter("address", "newSequencer", 2, true )]
        public virtual string NewSequencer { get; set; }
    }
}
