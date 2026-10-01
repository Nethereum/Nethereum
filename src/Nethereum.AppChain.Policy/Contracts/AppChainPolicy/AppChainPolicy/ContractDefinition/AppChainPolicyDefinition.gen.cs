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
        public static string BYTECODE = "0x60a060405234801561000f575f5ffd5b506040516109ae3803806109ae83398101604081905261002e916100aa565b60809384525f9190915560019081556040805160a0810182528281526201f40060208201819052620f42409282018390526301c9c380606083018190526001600160a01b0390951691909501819052600492909255600593909355600692909255600755600880546001600160a01b03191690911790556100f1565b5f5f5f5f608085870312156100bd575f5ffd5b845160208601519094506001600160a01b03811681146100db575f5ffd5b6040860151606090960151949790965092505050565b6080516108a66101085f395f60e801526108a65ff3fe608060405234801561000f575f5ffd5b50600436106100a6575f3560e01c8063900cf0cf1161006e578063900cf0cf1461011d57806395f4a3241461012657806399878105146101395780639ab229ea1461015c578063a4f21c931461016f578063c7d2985614610178575f5ffd5b8063083b1944146100aa578063229f1849146100c557806333787cf1146100ce57806383470923146100e357806384b76d9c1461010a575b5f5ffd5b6100b25f5481565b6040519081526020015b60405180910390f35b6100b260015481565b6100e16100dc366004610613565b6101cc565b005b6100b27f000000000000000000000000000000000000000000000000000000000000000081565b6100e161011836600461067d565b610261565b6100b260035481565b6100e16101343660046106bb565b6102d6565b61014c610147366004610744565b6103d0565b60405190151581526020016100bc565b6100e161016a3660046107c4565b610417565b6100b260025481565b60045460055460065460075460085461019b94939291906001600160a01b031685565b6040805195865260208601949094529284019190915260608301526001600160a01b0316608082015260a0016100bc565b6101da3360015484846104de565b6101ff5760405162461bcd60e51b81526004016101f690610812565b60405180910390fd5b60038054905f61020e83610838565b90915550505f848155600184905560025560035460408051868152602081018690527fcf1464c376cad5b10e549f3dff8356906a1d6182d697018ebf121e780e057ece910160405180910390a250505050565b61026f3360015484846104de565b61028b5760405162461bcd60e51b81526004016101f690610812565b60028390556040518381526001600160a01b0385169033907f49d648d4ef8266bd083ca38856e8a9975ca038a09ef3caefad72db512dd1a40e9060200160405180910390a350505050565b6102e3335f5486866104de565b61031e5760405162461bcd60e51b815260206004820152600c60248201526b2737ba1030903bb934ba32b960a11b60448201526064016101f6565b6002541561037a576103348660025484846104de565b1561037a5760405162461bcd60e51b8152602060048201526016602482015275125b9d9a5d1959481a5cc8189b1858dadb1a5cdd195960521b60448201526064016101f6565b5f8590556003546040805187815260208101929092526001600160a01b0388169133917fc4d4159ec5d71afc2086aadefde0ed5163e41d0d355a2a7f8d772f67c2965205910160405180910390a3505050505050565b5f5f6103df875f5488886104de565b6002549091505f90158015906103fe57506103fe8860025487876104de565b905081801561040b575080155b98975050505050505050565b6104253360015484846104de565b6104415760405162461bcd60e51b81526004016101f690610812565b60048054905f61045083610838565b9091555050600586905560068590556007849055600880546001600160a01b0319166001600160a01b03851690811790915560045460408051828152602081018a9052908101889052606081018790526080810192909252907fe59973f1beda445bb418f50e40f7c9b18a16ff3282df9670446d45a23f5c8a409060a00160405180910390a2505050505050565b5f836104ec575060016105c3565b6040516bffffffffffffffffffffffff19606087901b1660208201525f9060340160408051601f1981840301815291905280516020909101209050805f5b848110156105bc575f8686838181106105455761054561085c565b9050602002013590508083116105865760408051602081018590529081018290526060016040516020818303038152906040528051906020012092506105b3565b60408051602081018390529081018490526060016040516020818303038152906040528051906020012092505b5060010161052a565b5085149150505b949350505050565b5f5f83601f8401126105db575f5ffd5b50813567ffffffffffffffff8111156105f2575f5ffd5b6020830191508360208260051b850101111561060c575f5ffd5b9250929050565b5f5f5f5f60608587031215610626575f5ffd5b8435935060208501359250604085013567ffffffffffffffff81111561064a575f5ffd5b610656878288016105cb565b95989497509550505050565b80356001600160a01b0381168114610678575f5ffd5b919050565b5f5f5f5f60608587031215610690575f5ffd5b61069985610662565b935060208501359250604085013567ffffffffffffffff81111561064a575f5ffd5b5f5f5f5f5f5f608087890312156106d0575f5ffd5b6106d987610662565b955060208701359450604087013567ffffffffffffffff8111156106fb575f5ffd5b61070789828a016105cb565b909550935050606087013567ffffffffffffffff811115610726575f5ffd5b61073289828a016105cb565b979a9699509497509295939492505050565b5f5f5f5f5f60608688031215610758575f5ffd5b61076186610662565b9450602086013567ffffffffffffffff81111561077c575f5ffd5b610788888289016105cb565b909550935050604086013567ffffffffffffffff8111156107a7575f5ffd5b6107b3888289016105cb565b969995985093965092949392505050565b5f5f5f5f5f5f60a087890312156107d9575f5ffd5b8635955060208701359450604087013593506107f760608801610662565b9250608087013567ffffffffffffffff811115610726575f5ffd5b6020808252600c908201526b2737ba1030b71030b236b4b760a11b604082015260600190565b5f6001820161085557634e487b7160e01b5f52601160045260245ffd5b5060010190565b634e487b7160e01b5f52603260045260245ffdfea264697066735822122076a3401a27381d755720b28cb68ec4bdac2c21a28703e7402d95aa19a56544b564736f6c634300081c0033";
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
        [Parameter("bytes32[]", "proofInviteeNotBlacklisted", 4)]
        public virtual List<byte[]> ProofInviteeNotBlacklisted { get; set; }
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
