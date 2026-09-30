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
        public static string BYTECODE = "0x60a060405234801561000f575f5ffd5b506040516108e43803806108e483398101604081905261002e916100aa565b60809384525f9190915560019081556040805160a0810182528281526201f40060208201819052620f42409282018390526301c9c380606083018190526001600160a01b0390951691909501819052600492909255600593909355600692909255600755600880546001600160a01b03191690911790556100f1565b5f5f5f5f608085870312156100bd575f5ffd5b845160208601519094506001600160a01b03811681146100db575f5ffd5b6040860151606090960151949790965092505050565b6080516107dc6101085f395f60fb01526107dc5ff3fe608060405234801561000f575f5ffd5b50600436106100a6575f3560e01c806384b76d9c1161006e57806384b76d9c1461011d578063900cf0cf1461013057806399878105146101395780639ab229ea1461015c578063a4f21c931461016f578063c7d2985614610178575f5ffd5b8063083b1944146100aa5780631a5525a6146100c5578063229f1849146100da57806333787cf1146100e357806383470923146100f6575b5f5ffd5b6100b25f5481565b6040519081526020015b60405180910390f35b6100d86100d33660046105cf565b6101cc565b005b6100b260015481565b6100d86100f1366004610625565b61026e565b6100b27f000000000000000000000000000000000000000000000000000000000000000081565b6100d861012b3660046105cf565b6102fa565b6100b260035481565b61014c61014736600461065c565b610366565b60405190151581526020016100bc565b6100d861016a3660046106dc565b6103b8565b6100b260025481565b60045460055460065460075460085461019b94939291906001600160a01b031685565b6040805195865260208601949094529284019190915260608301526001600160a01b0316608082015260a0016100bc565b6101d9335f54848461047f565b6102195760405162461bcd60e51b815260206004820152600c60248201526b2737ba1030903bb934ba32b960a11b60448201526064015b60405180910390fd5b5f8390556003546040805185815260208101929092526001600160a01b0386169133917fc4d4159ec5d71afc2086aadefde0ed5163e41d0d355a2a7f8d772f67c296520591015b60405180910390a350505050565b61027c33600154848461047f565b6102985760405162461bcd60e51b815260040161021090610748565b60038054905f6102a78361076e565b90915550505f848155600184905560025560035460408051868152602081018690527fcf1464c376cad5b10e549f3dff8356906a1d6182d697018ebf121e780e057ece910160405180910390a250505050565b61030833600154848461047f565b6103245760405162461bcd60e51b815260040161021090610748565b60028390556040518381526001600160a01b0385169033907f49d648d4ef8266bd083ca38856e8a9975ca038a09ef3caefad72db512dd1a40e90602001610260565b5f5f610375875f54888861047f565b6002549091505f901580159061038a57508315155b801561039f575061039f88600254878761047f565b90508180156103ac575080155b98975050505050505050565b6103c633600154848461047f565b6103e25760405162461bcd60e51b815260040161021090610748565b60048054905f6103f18361076e565b9091555050600586905560068590556007849055600880546001600160a01b0319166001600160a01b03851690811790915560045460408051828152602081018a9052908101889052606081018790526080810192909252907fe59973f1beda445bb418f50e40f7c9b18a16ff3282df9670446d45a23f5c8a409060a00160405180910390a2505050505050565b5f8361048d57506001610564565b6040516bffffffffffffffffffffffff19606087901b1660208201525f9060340160408051601f1981840301815291905280516020909101209050805f5b8481101561055d575f8686838181106104e6576104e6610792565b905060200201359050808311610527576040805160208101859052908101829052606001604051602081830303815290604052805190602001209250610554565b60408051602081018390529081018490526060016040516020818303038152906040528051906020012092505b506001016104cb565b5085149150505b949350505050565b80356001600160a01b0381168114610582575f5ffd5b919050565b5f5f83601f840112610597575f5ffd5b50813567ffffffffffffffff8111156105ae575f5ffd5b6020830191508360208260051b85010111156105c8575f5ffd5b9250929050565b5f5f5f5f606085870312156105e2575f5ffd5b6105eb8561056c565b935060208501359250604085013567ffffffffffffffff81111561060d575f5ffd5b61061987828801610587565b95989497509550505050565b5f5f5f5f60608587031215610638575f5ffd5b8435935060208501359250604085013567ffffffffffffffff81111561060d575f5ffd5b5f5f5f5f5f60608688031215610670575f5ffd5b6106798661056c565b9450602086013567ffffffffffffffff811115610694575f5ffd5b6106a088828901610587565b909550935050604086013567ffffffffffffffff8111156106bf575f5ffd5b6106cb88828901610587565b969995985093965092949392505050565b5f5f5f5f5f5f60a087890312156106f1575f5ffd5b86359550602087013594506040870135935061070f6060880161056c565b9250608087013567ffffffffffffffff81111561072a575f5ffd5b61073689828a01610587565b979a9699509497509295939492505050565b6020808252600c908201526b2737ba1030b71030b236b4b760a11b604082015260600190565b5f6001820161078b57634e487b7160e01b5f52601160045260245ffd5b5060010190565b634e487b7160e01b5f52603260045260245ffdfea264697066735822122006fca3f8b57f186d6261afc76189cf447422e0dc9a7ed96dc85d7f2c47ab768864736f6c634300081c0033";
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
