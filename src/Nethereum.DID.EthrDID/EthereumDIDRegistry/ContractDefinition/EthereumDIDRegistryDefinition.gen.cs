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
using Nethereum.DID.EthrDID.EthereumDIDRegistry.ContractDefinition;

namespace Nethereum.DID.EthrDID.EthereumDIDRegistry.ContractDefinition
{


    public partial class EthereumDIDRegistryDeployment : EthereumDIDRegistryDeploymentBase
    {
        public EthereumDIDRegistryDeployment() : base(BYTECODE) { }
        public EthereumDIDRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class EthereumDIDRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080806040523460155761119d908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c8062c023da14610dfa578063022914a714610dbb5780630d44625b14610d72578063123b5e9814610be4578063240cf1fa14610aaa578063622b2a3c14610a43578063655a4ebf146109b957806370ae92d2146109815780637ad4b0a4146108e157806380b29f7c146108ad5780638733d4e814610878578063889c10dc14610821578063930726841461071a5780639c2c1b2b146105fc578063a7068d6614610509578063bd74c4e714610441578063e476af5c146102d3578063f11c2cec1461022d578063f96d0f9f146101f55763f976104f146100f4575f80fd5b346101f15760803660031901126101f15761010d610e8e565b610115610ea4565b5f5160206111285f395f51905f5260443592610157610132610eba565b936001600160a01b036101448461102e565b6001600160a01b03909216911614610fd2565b60018060a01b0316928392835f52600160205260405f20604051602081019084825260208152610188604082610efc565b5190205f908152602091825260408082206001600160a01b0394909416808352938352808220429081905587835260028452918190205481519586529285019390935291830191909152606082015280608081015b0390a25f5260026020524360405f20555f80f35b5f80fd5b346101f15760203660031901126101f1576001600160a01b03610216610e8e565b165f526002602052602060405f2054604051908152f35b346101f15760603660031901126101f1576040610248610e8e565b7f38a5a6e68f30ed1ab45860a4afb34bcb2fc00f22ca462d249b8a8d40cda6f7a3610271610ea4565b9161027d610132610ee6565b6001600160a01b039081165f818152602081815286822080546001600160a01b031916969094169586179093556002835285902054855194855291840191909152928392a25f5260026020524360405f20555f80f35b346101f15760c03660031901126101f1576102ec610e8e565b6102f4610fc2565b60a43560843567ffffffffffffffff82116101f1576103255f5160206111485f395f51905f52923690600401610f32565b906001600160a01b036103378661102e565b165f5260036020526103f860405f2054956103e5604051966020880198601960f81b8a525f60218a01523060601b60228a0152603689015260018060a01b0383169889986001600160601b03198560601b1660568201526e7265766f6b6541747472696275746560881b606a8201528660798201526103d36099828a518060208d018484015e81015f838201520301601f198101835282610efc565b51902090606435906044359085611077565b906001600160a01b03906101449061102e565b835f52600260205260405f2054610421604051938493845260806020850152608084019061100a565b905f604084015260608301520390a25f5260026020524360405f20555f80f35b346101f15760a03660031901126101f15761045a610e8e565b610462610ea4565b9060643567ffffffffffffffff81116101f157610486610498913690600401610f32565b926001600160a01b036101448461102e565b5f5160206111485f395f51905f526104ea6104b560843542611056565b9260018060a01b0316938493845f52600260205260405f2054604051938493604435855260806020860152608085019061100a565b91604084015260608301520390a25f5260026020524360405f20555f80f35b346101f15760803660031901126101f157610522610e8e565b602435905f5160206111285f395f51905f5261053c610ee6565b6064359361055c6001600160a01b036105548661102e565b163314610fd2565b6101dd6105c361056c8742611056565b9560018060a01b0316968796875f52600160205260405f2060405160208101908782526020815261059e604082610efc565b5190205f5260205260405f2060018060a01b0387165f5260205260405f205542611056565b855f52600260205260405f20549060405194859485909493926060926080830196835260018060a01b0316602083015260408201520152565b346101f15760e03660031901126101f157610615610e8e565b61061d610fc2565b5f5160206111285f395f51905f52608435610636610ed0565b906101dd6105c360c4356001600160a01b036106518961102e565b165f5260036020526106ea60405f2054986103e56040519960208b019b601960f81b8d525f60218d01523060601b60228d015260368c015260018060a01b0383169b8c9b6001600160601b03198560601b1660568201526a61646444656c656761746560a81b606a8201528960758201526001600160601b03198b60601b1660958201528660a982015260a981526103d360c982610efc565b6106f48142611056565b875f52600160205260405f2060405160208101908782526020815261059e604082610efc565b346101f15760c03660031901126101f157610733610e8e565b61073b610fc2565b5f5160206111285f395f51905f52608435610754610ed0565b6001600160a01b036107658661102e565b165f5260036020526107fb60405f2054956103e5604051966020880198601960f81b8a525f60218a01523060601b60228a0152603689015260018060a01b0383169889986001600160601b03198560601b1660568201526d7265766f6b6544656c656761746560901b606a8201528760788201526001600160601b03198760601b166098820152608c81526103d360ac82610efc565b835f52600160205260405f20604051602081019084825260208152610188604082610efc565b346101f15760a03660031901126101f15761083a610e8e565b610842610ea4565b905f5160206111285f395f51905f5260443561085c610eba565b6084359490919061055c906001600160a01b036101448761102e565b346101f15760203660031901126101f157602061089b610896610e8e565b61102e565b6040516001600160a01b039091168152f35b346101f1575f5160206111285f395f51905f526108c936610f88565b909290916101576001600160a01b036105548361102e565b346101f15760803660031901126101f1576108fa610e8e565b60443567ffffffffffffffff81116101f15761091a903690600401610f32565b9061092f6001600160a01b036105548361102e565b5f5160206111485f395f51905f526104ea61094c60643542611056565b9260018060a01b0316938493845f52600260205260405f2054604051938493602435855260806020860152608085019061100a565b346101f15760203660031901126101f1576001600160a01b036109a2610e8e565b165f526003602052602060405f2054604051908152f35b346101f15760803660031901126101f1576109d2610e8e565b6109da610ea4565b60643567ffffffffffffffff81116101f15761042192610a0f6101325f5160206111485f395f51905f52933690600401610f32565b60018060a01b0316928392835f52600260205260405f2054604051928392604435845260806020850152608084019061100a565b346101f157610a5136610f88565b9160018060a01b03165f52600160205260405f20906040516020810191825260208152610a7f604082610efc565b5190205f5260205260405f209060018060a01b03165f52602052602060405f20546040519042108152f35b346101f15760a03660031901126101f157610ac3610e8e565b610acb610fc2565b90608435906001600160a01b038216908183036101f1577f38a5a6e68f30ed1ab45860a4afb34bcb2fc00f22ca462d249b8a8d40cda6f7a391604091610ba4906001600160a01b03610b1c8261102e565b165f5260036020526103e5845f20549785519860208a0190601960f81b82525f60218c01523060601b60228c015260368b015260018060a01b038416998a996001600160601b03198660601b1660568301526a31b430b733b2a7bbb732b960a91b606a8301526001600160601b03199060601b166075820152606981526103d3608982610efc565b5f848152602081815283822080546001600160a01b03191684179055600281529083902054835192835290820152a25f5260026020524360405f20555f80f35b346101f15760e03660031901126101f157610bfd610e8e565b610c05610fc2565b60843560a43567ffffffffffffffff81116101f157610c28903690600401610f32565b9060c4356001600160a01b03610c3d8661102e565b165f52600360205260405f205494604051946020860196601960f81b8852602187015f90523060601b60228801526036870152600160a01b6001900382169687966001600160601b03198460601b166056820152606a81016b73657441747472696275746560a01b90528560768201528087518060208a01609684015e810186609682015203609601808252602001610cd69082610efc565b519020606435604435610ce99385611077565b906001600160a01b0390610cfc9061102e565b610d11926001600160a01b0316911614610fd2565b610d1b9042611056565b835f52600260205260405f20546040519384938452602084016080905260808401610d459161100a565b9160408401526060830152035f5160206111485f395f51905f5291a25f5260026020524360405f20555f80f35b346101f157610d8036610f88565b9160018060a01b03165f52600160205260405f20905f5260205260405f209060018060a01b03165f52602052602060405f2054604051908152f35b346101f15760203660031901126101f1576001600160a01b03610ddc610e8e565b165f525f602052602060018060a01b0360405f205416604051908152f35b346101f15760603660031901126101f157610e13610e8e565b6044359067ffffffffffffffff82116101f1575f5160206111485f395f51905f52610e45610421933690600401610f32565b91610e5a6001600160a01b036105548361102e565b60018060a01b0316928392835f52600260205260405f2054604051928392602435845260806020850152608084019061100a565b600435906001600160a01b03821682036101f157565b602435906001600160a01b03821682036101f157565b606435906001600160a01b03821682036101f157565b60a435906001600160a01b03821682036101f157565b604435906001600160a01b03821682036101f157565b90601f8019910116810190811067ffffffffffffffff821117610f1e57604052565b634e487b7160e01b5f52604160045260245ffd5b81601f820112156101f15780359067ffffffffffffffff8211610f1e5760405192610f67601f8401601f191660200185610efc565b828452602083830101116101f157815f926020809301838601378301015290565b60609060031901126101f1576004356001600160a01b03811681036101f15790602435906044356001600160a01b03811681036101f15790565b6024359060ff821682036101f157565b15610fd957565b60405162461bcd60e51b81526020600482015260096024820152683130b22fb0b1ba37b960b91b6044820152606490fd5b805180835260209291819084018484015e5f828201840152601f01601f1916010190565b6001600160a01b038181165f908152602081905260409020541680611051575090565b905090565b9190820180921161106357565b634e487b7160e01b5f52601160045260245ffd5b936020935f9360ff60809460405194855216868401526040830152606082015282805260015afa1561111c575f51906001600160a01b03906110b89061102e565b6001600160a01b038316911681036110e7575f52600360205260405f2080545f19811461106357600101905590565b60405162461bcd60e51b815260206004820152600d60248201526c6261645f7369676e617475726560981b6044820152606490fd5b6040513d5f823e3d90fdfe5a5084339536bcab65f20799fcc58724588145ca054bd2be626174b27ba156f718ab6b2ae3d64306c00ce663125f2bd680e441a098de1635bd7ad8b0d44965e4a26469706673582212202c6daff340ed39842eaaea641c326c486bd1d7b10d0f209919aa4e99885f540664736f6c634300081c0033";
        public EthereumDIDRegistryDeploymentBase() : base(BYTECODE) { }
        public EthereumDIDRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AddDelegate1Function : AddDelegate1FunctionBase { }

    [Function("addDelegate")]
    public class AddDelegate1FunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("address", "actor", 2)]
        public virtual string Actor { get; set; }
        [Parameter("bytes32", "delegateType", 3)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 4)]
        public virtual string Delegate { get; set; }
        [Parameter("uint256", "validity", 5)]
        public virtual BigInteger Validity { get; set; }
    }

    public partial class AddDelegateFunction : AddDelegateFunctionBase { }

    [Function("addDelegate")]
    public class AddDelegateFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "delegateType", 2)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 3)]
        public virtual string Delegate { get; set; }
        [Parameter("uint256", "validity", 4)]
        public virtual BigInteger Validity { get; set; }
    }

    public partial class AddDelegateSignedFunction : AddDelegateSignedFunctionBase { }

    [Function("addDelegateSigned")]
    public class AddDelegateSignedFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("uint8", "sigV", 2)]
        public virtual byte SigV { get; set; }
        [Parameter("bytes32", "sigR", 3)]
        public virtual byte[] SigR { get; set; }
        [Parameter("bytes32", "sigS", 4)]
        public virtual byte[] SigS { get; set; }
        [Parameter("bytes32", "delegateType", 5)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 6)]
        public virtual string Delegate { get; set; }
        [Parameter("uint256", "validity", 7)]
        public virtual BigInteger Validity { get; set; }
    }

    public partial class ChangeOwnerFunction : ChangeOwnerFunctionBase { }

    [Function("changeOwner")]
    public class ChangeOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("address", "actor", 2)]
        public virtual string Actor { get; set; }
        [Parameter("address", "newOwner", 3)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ChangeOwnerSignedFunction : ChangeOwnerSignedFunctionBase { }

    [Function("changeOwnerSigned")]
    public class ChangeOwnerSignedFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("uint8", "sigV", 2)]
        public virtual byte SigV { get; set; }
        [Parameter("bytes32", "sigR", 3)]
        public virtual byte[] SigR { get; set; }
        [Parameter("bytes32", "sigS", 4)]
        public virtual byte[] SigS { get; set; }
        [Parameter("address", "newOwner", 5)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ChangedFunction : ChangedFunctionBase { }

    [Function("changed", "uint256")]
    public class ChangedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class DelegatesFunction : DelegatesFunctionBase { }

    [Function("delegates", "uint256")]
    public class DelegatesFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
    }

    public partial class IdentityOwnerFunction : IdentityOwnerFunctionBase { }

    [Function("identityOwner", "address")]
    public class IdentityOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
    }

    public partial class NonceFunction : NonceFunctionBase { }

    [Function("nonce", "uint256")]
    public class NonceFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OwnersFunction : OwnersFunctionBase { }

    [Function("owners", "address")]
    public class OwnersFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RevokeAttributeFunction : RevokeAttributeFunctionBase { }

    [Function("revokeAttribute")]
    public class RevokeAttributeFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "name", 2)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 3)]
        public virtual byte[] Value { get; set; }
    }

    public partial class RevokeAttribute1Function : RevokeAttribute1FunctionBase { }

    [Function("revokeAttribute")]
    public class RevokeAttribute1FunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("address", "actor", 2)]
        public virtual string Actor { get; set; }
        [Parameter("bytes32", "name", 3)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 4)]
        public virtual byte[] Value { get; set; }
    }

    public partial class RevokeAttributeSignedFunction : RevokeAttributeSignedFunctionBase { }

    [Function("revokeAttributeSigned")]
    public class RevokeAttributeSignedFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("uint8", "sigV", 2)]
        public virtual byte SigV { get; set; }
        [Parameter("bytes32", "sigR", 3)]
        public virtual byte[] SigR { get; set; }
        [Parameter("bytes32", "sigS", 4)]
        public virtual byte[] SigS { get; set; }
        [Parameter("bytes32", "name", 5)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 6)]
        public virtual byte[] Value { get; set; }
    }

    public partial class RevokeDelegateFunction : RevokeDelegateFunctionBase { }

    [Function("revokeDelegate")]
    public class RevokeDelegateFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "delegateType", 2)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 3)]
        public virtual string Delegate { get; set; }
    }

    public partial class RevokeDelegate1Function : RevokeDelegate1FunctionBase { }

    [Function("revokeDelegate")]
    public class RevokeDelegate1FunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("address", "actor", 2)]
        public virtual string Actor { get; set; }
        [Parameter("bytes32", "delegateType", 3)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 4)]
        public virtual string Delegate { get; set; }
    }

    public partial class RevokeDelegateSignedFunction : RevokeDelegateSignedFunctionBase { }

    [Function("revokeDelegateSigned")]
    public class RevokeDelegateSignedFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("uint8", "sigV", 2)]
        public virtual byte SigV { get; set; }
        [Parameter("bytes32", "sigR", 3)]
        public virtual byte[] SigR { get; set; }
        [Parameter("bytes32", "sigS", 4)]
        public virtual byte[] SigS { get; set; }
        [Parameter("bytes32", "delegateType", 5)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 6)]
        public virtual string Delegate { get; set; }
    }

    public partial class SetAttributeFunction : SetAttributeFunctionBase { }

    [Function("setAttribute")]
    public class SetAttributeFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "name", 2)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 3)]
        public virtual byte[] Value { get; set; }
        [Parameter("uint256", "validity", 4)]
        public virtual BigInteger Validity { get; set; }
    }

    public partial class SetAttribute1Function : SetAttribute1FunctionBase { }

    [Function("setAttribute")]
    public class SetAttribute1FunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("address", "actor", 2)]
        public virtual string Actor { get; set; }
        [Parameter("bytes32", "name", 3)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 4)]
        public virtual byte[] Value { get; set; }
        [Parameter("uint256", "validity", 5)]
        public virtual BigInteger Validity { get; set; }
    }

    public partial class SetAttributeSignedFunction : SetAttributeSignedFunctionBase { }

    [Function("setAttributeSigned")]
    public class SetAttributeSignedFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("uint8", "sigV", 2)]
        public virtual byte SigV { get; set; }
        [Parameter("bytes32", "sigR", 3)]
        public virtual byte[] SigR { get; set; }
        [Parameter("bytes32", "sigS", 4)]
        public virtual byte[] SigS { get; set; }
        [Parameter("bytes32", "name", 5)]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 6)]
        public virtual byte[] Value { get; set; }
        [Parameter("uint256", "validity", 7)]
        public virtual BigInteger Validity { get; set; }
    }

    public partial class ValidDelegateFunction : ValidDelegateFunctionBase { }

    [Function("validDelegate", "bool")]
    public class ValidDelegateFunctionBase : FunctionMessage
    {
        [Parameter("address", "identity", 1)]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "delegateType", 2)]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 3)]
        public virtual string Delegate { get; set; }
    }











    public partial class ChangedOutputDTO : ChangedOutputDTOBase { }

    [FunctionOutput]
    public class ChangedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class DelegatesOutputDTO : DelegatesOutputDTOBase { }

    [FunctionOutput]
    public class DelegatesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class IdentityOwnerOutputDTO : IdentityOwnerOutputDTOBase { }

    [FunctionOutput]
    public class IdentityOwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class NonceOutputDTO : NonceOutputDTOBase { }

    [FunctionOutput]
    public class NonceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class OwnersOutputDTO : OwnersOutputDTOBase { }

    [FunctionOutput]
    public class OwnersOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



















    public partial class ValidDelegateOutputDTO : ValidDelegateOutputDTOBase { }

    [FunctionOutput]
    public class ValidDelegateOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class DIDAttributeChangedEventDTO : DIDAttributeChangedEventDTOBase { }

    [Event("DIDAttributeChanged")]
    public class DIDAttributeChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "identity", 1, true )]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "name", 2, false )]
        public virtual byte[] Name { get; set; }
        [Parameter("bytes", "value", 3, false )]
        public virtual byte[] Value { get; set; }
        [Parameter("uint256", "validTo", 4, false )]
        public virtual BigInteger ValidTo { get; set; }
        [Parameter("uint256", "previousChange", 5, false )]
        public virtual BigInteger PreviousChange { get; set; }
    }

    public partial class DIDDelegateChangedEventDTO : DIDDelegateChangedEventDTOBase { }

    [Event("DIDDelegateChanged")]
    public class DIDDelegateChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "identity", 1, true )]
        public virtual string Identity { get; set; }
        [Parameter("bytes32", "delegateType", 2, false )]
        public virtual byte[] DelegateType { get; set; }
        [Parameter("address", "delegate_", 3, false )]
        public virtual string Delegate { get; set; }
        [Parameter("uint256", "validTo", 4, false )]
        public virtual BigInteger ValidTo { get; set; }
        [Parameter("uint256", "previousChange", 5, false )]
        public virtual BigInteger PreviousChange { get; set; }
    }

    public partial class DIDOwnerChangedEventDTO : DIDOwnerChangedEventDTOBase { }

    [Event("DIDOwnerChanged")]
    public class DIDOwnerChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "identity", 1, true )]
        public virtual string Identity { get; set; }
        [Parameter("address", "owner", 2, false )]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "previousChange", 3, false )]
        public virtual BigInteger PreviousChange { get; set; }
    }
}
