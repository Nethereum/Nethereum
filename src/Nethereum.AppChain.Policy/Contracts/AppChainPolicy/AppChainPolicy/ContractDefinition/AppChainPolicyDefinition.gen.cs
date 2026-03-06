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
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy.ContractDefinition;

namespace Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy.ContractDefinition
{


    public partial class AppChainPolicyDeployment : AppChainPolicyDeploymentBase
    {
        public AppChainPolicyDeployment() : base(BYTECODE) { }
        public AppChainPolicyDeployment(string byteCode) : base(byteCode) { }
    }

    public class AppChainPolicyDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a060405234801561001057600080fd5b5060405161092138038061092183398101604081905261002f916100ac565b608093845260009190915560019081556040805160a0810182528281526201f40060208201819052620f42409282018390526301c9c380606083018190526001600160a01b0390951691909501819052600492909255600593909355600692909255600755600880546001600160a01b03191690911790556100f7565b600080600080608085870312156100c257600080fd5b845160208601519094506001600160a01b03811681146100e157600080fd5b6040860151606090960151949790965092505050565b60805161080f6101126000396000610100015261080f6000f3fe608060405234801561001057600080fd5b50600436106100a95760003560e01c806384b76d9c1161007157806384b76d9c14610122578063900cf0cf14610135578063998781051461013e5780639ab229ea14610161578063a4f21c9314610174578063c7d298561461017d57600080fd5b8063083b1944146100ae5780631a5525a6146100ca578063229f1849146100df57806333787cf1146100e857806383470923146100fb575b600080fd5b6100b760005481565b6040519081526020015b60405180910390f35b6100dd6100d83660046105ef565b6101d1565b005b6100b760015481565b6100dd6100f6366004610649565b610275565b6100b77f000000000000000000000000000000000000000000000000000000000000000081565b6100dd6101303660046105ef565b610303565b6100b760035481565b61015161014c366004610684565b61036f565b60405190151581526020016100c1565b6100dd61016f366004610705565b6103c4565b6100b760025481565b6004546005546006546007546008546101a094939291906001600160a01b031685565b6040805195865260208601949094529284019190915260608301526001600160a01b0316608082015260a0016100c1565b6101df33600054848461048c565b61021f5760405162461bcd60e51b815260206004820152600c60248201526b2737ba1030903bb934ba32b960a11b60448201526064015b60405180910390fd5b60008390556003546040805185815260208101929092526001600160a01b0386169133917fc4d4159ec5d71afc2086aadefde0ed5163e41d0d355a2a7f8d772f67c296520591015b60405180910390a350505050565b61028333600154848461048c565b61029f5760405162461bcd60e51b815260040161021690610776565b600380549060006102af8361079c565b90915550506000848155600184905560025560035460408051868152602081018690527fcf1464c376cad5b10e549f3dff8356906a1d6182d697018ebf121e780e057ece910160405180910390a250505050565b61031133600154848461048c565b61032d5760405162461bcd60e51b815260040161021690610776565b60028390556040518381526001600160a01b0385169033907f49d648d4ef8266bd083ca38856e8a9975ca038a09ef3caefad72db512dd1a40e90602001610267565b60008061038087600054888861048c565b6002549091506000901580159061039657508315155b80156103ab57506103ab88600254878761048c565b90508180156103b8575080155b98975050505050505050565b6103d233600154848461048c565b6103ee5760405162461bcd60e51b815260040161021690610776565b600480549060006103fe8361079c565b9091555050600586905560068590556007849055600880546001600160a01b0319166001600160a01b03851690811790915560045460408051828152602081018a9052908101889052606081018790526080810192909252907fe59973f1beda445bb418f50e40f7c9b18a16ff3282df9670446d45a23f5c8a409060a00160405180910390a2505050505050565b60008361049b5750600161057f565b6040516bffffffffffffffffffffffff19606087901b16602082015260009060340160408051601f19818403018152919052805160209091012090508060005b848110156105785760008686838181106104f7576104f76107c3565b905060200201359050808311610538576040805160208101859052908101829052606001604051602081830303815290604052805190602001209250610565565b60408051602081018390529081018490526060016040516020818303038152906040528051906020012092505b50806105708161079c565b9150506104db565b5085149150505b949350505050565b80356001600160a01b038116811461059e57600080fd5b919050565b60008083601f8401126105b557600080fd5b50813567ffffffffffffffff8111156105cd57600080fd5b6020830191508360208260051b85010111156105e857600080fd5b9250929050565b6000806000806060858703121561060557600080fd5b61060e85610587565b935060208501359250604085013567ffffffffffffffff81111561063157600080fd5b61063d878288016105a3565b95989497509550505050565b6000806000806060858703121561065f57600080fd5b8435935060208501359250604085013567ffffffffffffffff81111561063157600080fd5b60008060008060006060868803121561069c57600080fd5b6106a586610587565b9450602086013567ffffffffffffffff808211156106c257600080fd5b6106ce89838a016105a3565b909650945060408801359150808211156106e757600080fd5b506106f4888289016105a3565b969995985093965092949392505050565b60008060008060008060a0878903121561071e57600080fd5b86359550602087013594506040870135935061073c60608801610587565b9250608087013567ffffffffffffffff81111561075857600080fd5b61076489828a016105a3565b979a9699509497509295939492505050565b6020808252600c908201526b2737ba1030b71030b236b4b760a11b604082015260600190565b6000600182016107bc57634e487b7160e01b600052601160045260246000fd5b5060010190565b634e487b7160e01b600052603260045260246000fdfea2646970667358221220a07983aee3ea700aa87af500f084d67a5f0af00916dd3b39c60dff5791aec74a64736f6c63430008130033";
        public AppChainPolicyDeploymentBase() : base(BYTECODE) { }
        public AppChainPolicyDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("uint256", "_appChainId", 1)]
        public virtual BigInteger AppChainId { get; set; }
        [Parameter("address", "_sequencer", 2)]
        public virtual string Sequencer { get; set; }
        [Parameter("bytes32", "_initialWritersRoot", 3)]
        public virtual byte[] InitialWritersRoot { get; set; }
        [Parameter("bytes32", "_initialAdminsRoot", 4)]
        public virtual byte[] InitialAdminsRoot { get; set; }
    }

    public partial class AdminsRootFunction : AdminsRootFunctionBase { }

    [Function("adminsRoot", "bytes32")]
    public class AdminsRootFunctionBase : FunctionMessage
    {

    }

    public partial class AppChainIdFunction : AppChainIdFunctionBase { }

    [Function("appChainId", "uint256")]
    public class AppChainIdFunctionBase : FunctionMessage
    {

    }

    public partial class BanFunction : BanFunctionBase { }

    [Function("ban")]
    public class BanFunctionBase : FunctionMessage
    {
        [Parameter("address", "toBan", 1)]
        public virtual string ToBan { get; set; }
        [Parameter("bytes32", "newBlacklistRoot", 2)]
        public virtual byte[] NewBlacklistRoot { get; set; }
        [Parameter("bytes32[]", "proofCallerIsAdmin", 3)]
        public virtual List<byte[]> ProofCallerIsAdmin { get; set; }
    }

    public partial class BlacklistRootFunction : BlacklistRootFunctionBase { }

    [Function("blacklistRoot", "bytes32")]
    public class BlacklistRootFunctionBase : FunctionMessage
    {

    }

    public partial class CurrentPolicyFunction : CurrentPolicyFunctionBase { }

    [Function("currentPolicy", typeof(CurrentPolicyOutputDTO))]
    public class CurrentPolicyFunctionBase : FunctionMessage
    {

    }

    public partial class EpochFunction : EpochFunctionBase { }

    [Function("epoch", "uint256")]
    public class EpochFunctionBase : FunctionMessage
    {

    }

    public partial class InviteFunction : InviteFunctionBase { }

    [Function("invite")]
    public class InviteFunctionBase : FunctionMessage
    {
        [Parameter("address", "invitee", 1)]
        public virtual string Invitee { get; set; }
        [Parameter("bytes32", "newWritersRoot", 2)]
        public virtual byte[] NewWritersRoot { get; set; }
        [Parameter("bytes32[]", "proofCallerIsWriter", 3)]
        public virtual List<byte[]> ProofCallerIsWriter { get; set; }
    }

    public partial class IsValidWriterFunction : IsValidWriterFunctionBase { }

    [Function("isValidWriter", "bool")]
    public class IsValidWriterFunctionBase : FunctionMessage
    {
        [Parameter("address", "addr", 1)]
        public virtual string Addr { get; set; }
        [Parameter("bytes32[]", "writerProof", 2)]
        public virtual List<byte[]> WriterProof { get; set; }
        [Parameter("bytes32[]", "blacklistProof", 3)]
        public virtual List<byte[]> BlacklistProof { get; set; }
    }

    public partial class RebuildTreesFunction : RebuildTreesFunctionBase { }

    [Function("rebuildTrees")]
    public class RebuildTreesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "newWritersRoot", 1)]
        public virtual byte[] NewWritersRoot { get; set; }
        [Parameter("bytes32", "newAdminsRoot", 2)]
        public virtual byte[] NewAdminsRoot { get; set; }
        [Parameter("bytes32[]", "proofCallerIsAdmin", 3)]
        public virtual List<byte[]> ProofCallerIsAdmin { get; set; }
    }

    public partial class UpdatePolicyFunction : UpdatePolicyFunctionBase { }

    [Function("updatePolicy")]
    public class UpdatePolicyFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "maxCalldataBytes", 1)]
        public virtual BigInteger MaxCalldataBytes { get; set; }
        [Parameter("uint256", "maxLogBytes", 2)]
        public virtual BigInteger MaxLogBytes { get; set; }
        [Parameter("uint256", "blockGasLimit", 3)]
        public virtual BigInteger BlockGasLimit { get; set; }
        [Parameter("address", "sequencer", 4)]
        public virtual string Sequencer { get; set; }
        [Parameter("bytes32[]", "proofCallerIsAdmin", 5)]
        public virtual List<byte[]> ProofCallerIsAdmin { get; set; }
    }

    public partial class WritersRootFunction : WritersRootFunctionBase { }

    [Function("writersRoot", "bytes32")]
    public class WritersRootFunctionBase : FunctionMessage
    {

    }

    public partial class AdminsRootOutputDTO : AdminsRootOutputDTOBase { }

    [FunctionOutput]
    public class AdminsRootOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AppChainIdOutputDTO : AppChainIdOutputDTOBase { }

    [FunctionOutput]
    public class AppChainIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class BlacklistRootOutputDTO : BlacklistRootOutputDTOBase { }

    [FunctionOutput]
    public class BlacklistRootOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class CurrentPolicyOutputDTO : CurrentPolicyOutputDTOBase { }

    [FunctionOutput]
    public class CurrentPolicyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "version", 1)]
        public virtual BigInteger Version { get; set; }
        [Parameter("uint256", "maxCalldataBytes", 2)]
        public virtual BigInteger MaxCalldataBytes { get; set; }
        [Parameter("uint256", "maxLogBytes", 3)]
        public virtual BigInteger MaxLogBytes { get; set; }
        [Parameter("uint256", "blockGasLimit", 4)]
        public virtual BigInteger BlockGasLimit { get; set; }
        [Parameter("address", "sequencer", 5)]
        public virtual string Sequencer { get; set; }
    }

    public partial class EpochOutputDTO : EpochOutputDTOBase { }

    [FunctionOutput]
    public class EpochOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class IsValidWriterOutputDTO : IsValidWriterOutputDTOBase { }

    [FunctionOutput]
    public class IsValidWriterOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }





    public partial class WritersRootOutputDTO : WritersRootOutputDTOBase { }

    [FunctionOutput]
    public class WritersRootOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AdminAddedEventDTO : AdminAddedEventDTOBase { }

    [Event("AdminAdded")]
    public class AdminAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "addedBy", 1, true )]
        public virtual string AddedBy { get; set; }
        [Parameter("address", "admin", 2, true )]
        public virtual string Admin { get; set; }
        [Parameter("bytes32", "newAdminsRoot", 3, false )]
        public virtual byte[] NewAdminsRoot { get; set; }
    }

    public partial class MemberBannedEventDTO : MemberBannedEventDTOBase { }

    [Event("MemberBanned")]
    public class MemberBannedEventDTOBase : IEventDTO
    {
        [Parameter("address", "bannedBy", 1, true )]
        public virtual string BannedBy { get; set; }
        [Parameter("address", "banned", 2, true )]
        public virtual string Banned { get; set; }
        [Parameter("bytes32", "newBlacklistRoot", 3, false )]
        public virtual byte[] NewBlacklistRoot { get; set; }
    }

    public partial class MemberInvitedEventDTO : MemberInvitedEventDTOBase { }

    [Event("MemberInvited")]
    public class MemberInvitedEventDTOBase : IEventDTO
    {
        [Parameter("address", "inviter", 1, true )]
        public virtual string Inviter { get; set; }
        [Parameter("address", "invitee", 2, true )]
        public virtual string Invitee { get; set; }
        [Parameter("bytes32", "newRoot", 3, false )]
        public virtual byte[] NewRoot { get; set; }
        [Parameter("uint256", "epoch", 4, false )]
        public virtual BigInteger Epoch { get; set; }
    }

    public partial class PolicyChangedEventDTO : PolicyChangedEventDTOBase { }

    [Event("PolicyChanged")]
    public class PolicyChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "version", 1, true )]
        public virtual BigInteger Version { get; set; }
        [Parameter("tuple", "config", 2, false )]
        public virtual PolicyConfig Config { get; set; }
    }

    public partial class TreeRebuiltEventDTO : TreeRebuiltEventDTOBase { }

    [Event("TreeRebuilt")]
    public class TreeRebuiltEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "newEpoch", 1, true )]
        public virtual BigInteger NewEpoch { get; set; }
        [Parameter("bytes32", "newWritersRoot", 2, false )]
        public virtual byte[] NewWritersRoot { get; set; }
        [Parameter("bytes32", "newAdminsRoot", 3, false )]
        public virtual byte[] NewAdminsRoot { get; set; }
    }
}
