using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;

namespace Nethereum.Uniswap.Permit2.ContractDefinition
{


    public partial class Permit2Deployment : Permit2DeploymentBase
    {
        public Permit2Deployment() : base(BYTECODE) { }
        public Permit2Deployment(string byteCode) : base(byteCode) { }
    }

    public class Permit2DeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60c03460b7574660a052602081017f8cad95687ba82c2ce50e74f7b754645e5117c3a5bec8151c0726d5857980a86681527f9ac997416e8ff9d2ff6bebeb7149f65cdae5e32e2b90440b566bb3044041d36a60408301524660608301523060808301526080825260a082019180831060018060401b0384111760a35782604052519020608052611aa890816100bc82396080518161125d015260a051816112370152f35b634e487b7160e01b5f52604160045260245ffd5b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c80630d58b1db14610e4d578063137c29fe14610cbf5780632a2d80d114610aa65780632b67b570146109d357806330f28b7a146109095780633644e515146108ef57806336c78516146108a95780633ff9dcb1146108445780634fe02b441461080157806365d9723c146106d457806387517c45146105e4578063927da10514610562578063cc53287f14610485578063edd9444b14610351578063f09f29a0146102f45763fe8ec1a7146100c9575f80fd5b346102f05760c03660031901126102f0576004356001600160401b0381116102f0576100f9903690600401611161565b6024356001600160401b0381116102f057610118903690600401611131565b9091610122610fe0565b906084356001600160401b0381116102f057610142903690600401611082565b92909360a4356001600160401b0381116102f057610164903690600401611082565b9590946102336040519261017960a085610f7d565b606b8452602083818601927f5065726d697442617463685769746e6573735472616e7366657246726f6d285484527f6f6b656e5065726d697373696f6e735b5d207065726d69747465642c6164647260408801527f657373207370656e6465722c75696e74323536206e6f6e63652c75696e74323560608801526a0d88191958591b1a5b994b60aa1b6080880152604051958694848601985180918a5e8501918483015f81523701015f815203601f198101835282610f7d565b519020958351519661024488611466565b975f5b8181106102c85750506102c69760405161027781610269602082018095611498565b03601f198101835282610f7d565b519020602086810151604080890151815193840195865290830193909352336060830152608082015260a081019190915260643560c08201526102bd8160e08101610269565b51902093611842565b005b806102df6102d96001938a5161120c565b51611973565b6102e9828d61120c565b5201610247565b5f80fd5b346102f05760c03660031901126102f05760405161031181610f62565b61031c3660046110d9565b81526084356001600160a01b03811681036102f057816103499160208094015260a43560408201526117db565b604051908152f35b346102f05760803660031901126102f0576004356001600160401b0381116102f057610381903690600401611161565b6024356001600160401b0381116102f0576103a0903690600401611131565b6103ab929192610fe0565b916064356001600160401b0381116102f0576103cb903690600401611082565b939092825151956103db87611466565b5f5b88811061046357506102c697506040516103ff81610269602082018095611498565b519020602085015160408601516040519160208301937ffcf35f5ac6a2c28868dc44c302166470266239195f02b0ee408334829333b76685526040840152336060840152608083015260a08201526102bd60c082800301601f198101835282610f7d565b806104746102d9600193895161120c565b61047e828561120c565b52016103dd565b346102f05760203660031901126102f0576004356001600160401b0381116102f0576104b5903690600401611131565b905f5b8281106104c157005b806104d76104d260019386866112f1565b611301565b6104ed60206104e78488886112f1565b01611301565b335f8181526020868152604080832060a089901b899003968716808552908352818420959096168084529482529182902080546001600160a01b03191690558151948552840192909252917f89b1add15eff56b3dfe299ad94e01f2b52fbcb80ae1a3baea6ae8c04cb2b98a49190a2016104b8565b346102f05760603660031901126102f05761057b610fb4565b610583610fca565b61058b610fe0565b6001600160a01b039283165f90815260016020908152604080832094861683529381528382209285168252918252829020548251938116845265ffffffffffff60a082901c169184019190915260d01c90820152606090f35b346102f05760803660031901126102f0576105fd610fb4565b610605610fca565b61060d610fe0565b9060643565ffffffffffff8116908181036102f057335f9081526001602090815260408083206001600160a01b0389811685529083528184209087168452909152902090826106cf575065ffffffffffff42165b81546001600160d01b03191660a09190911b65ffffffffffff60a01b16176001600160a01b03948516908117909155604080519182526020820192909252918316939092169133917fda9fa7c1b00402c17d0161b249b1ab8bbec047c5a52207b9c112deffd817036b9190a4005b610661565b346102f05760603660031901126102f0576106ed610fb4565b6106f5610fca565b6044359065ffffffffffff8216918281036102f057335f9081526001602090815260408083206001600160a01b038881168552908352818420908616845290915290205460d01c90818411156107f25761ffff65ffffffffffff83830316116107e357335f8181526001602090815260408083206001600160a01b03998a16808552908352818420979099168084529682529182902080546001600160d01b031660d09590951b6001600160d01b0319169490941790935580519586529185019290925291939290917f55eb90d810e1700b35a8e7e25395ff7f2b2259abd7415ca2284dfb1c246418f391a4005b631269ad1360e11b5f5260045ffd5b633ab3447f60e11b5f5260045ffd5b346102f05760403660031901126102f0576001600160a01b03610822610fb4565b165f525f60205260405f206024355f52602052602060405f2054604051908152f35b346102f05760403660031901126102f057600435602435335f525f60205260405f20825f5260205260405f2081815417905560405191825260208201527f3704902f963766a4e561bbaab6e6cdc1b1dd12f6e9e99648da8843b3f46b918d60403392a2005b346102f05760803660031901126102f0576108c2610fb4565b6108ca610fca565b6108d2610fe0565b606435916001600160a01b03831683036102f0576102c693611315565b346102f0575f3660031901126102f0576020610349611234565b346102f0576101003660031901126102f05761092436611049565b60403660831901126102f057610938610f9e565b9060e435916001600160401b0383116102f05761095c6102c6933690600401611082565b9290916109698251611973565b602083015160408401516040519160208301937f939c21a48a8dbe3a9a2404a1d46691e4d39f6583d6ec6b35714604c986d8010685526040840152336060840152608083015260a08201526109ca60c082800301601f198101835282610f7d565b519020916113f6565b346102f0576101003660031901126102f0576109ed610fb4565b60c03660231901126102f05760405190610a0682610f62565b610a113660246110d9565b825260a435906001600160a01b03821682036102f05760208301918252604083019260c435845260e4356001600160401b0381116102f057610a57903690600401611082565b909451804211610a945750826102c695610a8292610a7c610a77866117db565b6114c5565b9161150e565b5191516001600160a01b0316916116db565b63cd21db4f60e01b5f5260045260245ffd5b346102f05760603660031901126102f057610abf610fb4565b6024356001600160401b0381116102f057606060031982360301126102f05760405190610aeb82610f62565b80600401356001600160401b0381116102f0578101366023820112156102f057600481013590610b1a826110af565b91610b286040519384610f7d565b808352602060048185019260071b84010101913683116102f057602401905b828210610ca55750505082526044610b6160248301610ff6565b91602084019283520135604083018181526044356001600160401b0381116102f057610b91903690600401611082565b919092804211610a94575091859185515193610bac85611466565b945f5b818110610c70575050610a7c90610c3795604051610bd581610269602082018095611498565b5190209060018060a01b0388511690516040519160208301937faf1b0d30d2cab0380e68f0689007e3254993c596f2fdd0aaa7f4d04f79440863855260408401526060830152608082015260808152610c2f60a082610f7d565b5190206114c5565b5181515191906001600160a01b03165f5b838110610c5157005b80610c6a8387610c64600195885161120c565b516116db565b01610c48565b600191929496939550610c8d610c87828b5161120c565b516119fb565b610c97828661120c565b520190889492959391610baf565b6020608091610cb436856110d9565b815201910190610b47565b346102f0576101403660031901126102f057610cda36611049565b60403660831901126102f057610cee610f9e565b90610104356001600160401b0381116102f057610d0f903690600401611082565b909161012435936001600160401b0385116102f057610d356102c6953690600401611082565b949093610dfd60405192610d4a60a085610f7d565b60648452602083818601927f5065726d69745769746e6573735472616e7366657246726f6d28546f6b656e5084527f65726d697373696f6e73207065726d69747465642c616464726573732073706560408801527f6e6465722c75696e74323536206e6f6e63652c75696e7432353620646561646c6060880152631a5b994b60e21b6080880152604051958694848601985180918a5e8501918483015f81523701015f815203601f198101835282610f7d565b519020610e0a8351611973565b602084810151604080870151815193840195865290830193909352336060830152608082015260a081019190915260e43560c08201526109ca8160e08101610269565b346102f05760203660031901126102f0576004356001600160401b0381116102f057366023820112156102f0578060040135906001600160401b0382116102f0573660248360071b830101116102f0575f5b828110156102c6578060071b820190608060231983360301126102f057610f2d600192604051610ece81610f33565b610eda60248301610ff6565b8082526060610eeb60448501610ff6565b92836020820152610f106084610f0360648801610ff6565b9687604085015201610ff6565b910181905260a087901b8790039081169381169281169116611315565b01610e9f565b608081019081106001600160401b03821117610f4e57604052565b634e487b7160e01b5f52604160045260245ffd5b606081019081106001600160401b03821117610f4e57604052565b90601f801991011681019081106001600160401b03821117610f4e57604052565b60c435906001600160a01b03821682036102f057565b600435906001600160a01b03821682036102f057565b602435906001600160a01b03821682036102f057565b604435906001600160a01b03821682036102f057565b35906001600160a01b03821682036102f057565b91908260409103126102f057604051604081018181106001600160401b03821117610f4e57604052602080829461104081610ff6565b84520135910152565b9060806003198301126102f05760405161106281610f62565b61106e8193600461100a565b815260443560208201526040606435910152565b9181601f840112156102f0578235916001600160401b0383116102f057602083818601950101116102f057565b6001600160401b038111610f4e5760051b60200190565b359065ffffffffffff821682036102f057565b91908260809103126102f0576040516110f181610f33565b606061112c81839561110281610ff6565b855261111060208201610ff6565b6020860152611121604082016110c6565b6040860152016110c6565b910152565b9181601f840112156102f0578235916001600160401b0383116102f0576020808501948460061b0101116102f057565b9190916060818403126102f0576040519061117b82610f62565b819381356001600160401b0381116102f057820181601f820112156102f05780356111a5816110af565b926111b36040519485610f7d565b81845260208085019260061b840101928184116102f057602001915b8383106111f2575050505060409182918452602081013560208501520135910152565b6020604091611201848661100a565b8152019201916111cf565b80518210156112205760209160051b010190565b634e487b7160e01b5f52603260045260245ffd5b467f00000000000000000000000000000000000000000000000000000000000000000361127f577f000000000000000000000000000000000000000000000000000000000000000090565b60405160208101907f8cad95687ba82c2ce50e74f7b754645e5117c3a5bec8151c0726d5857980a86682527f9ac997416e8ff9d2ff6bebeb7149f65cdae5e32e2b90440b566bb3044041d36a6040820152466060820152306080820152608081526112eb60a082610f7d565b51902090565b91908110156112205760061b0190565b356001600160a01b03811681036102f05790565b6001600160a01b038082165f90815260016020908152604080832093881683529281528282203383529052208054909594939060a081901c65ffffffffffff164281106113e457506001600160a01b0316956002600160a01b03198701611393575b50939450611391936001600160a01b0390811693166118f9565b565b6001600160a01b0382168710156113b7578663f96fb07160e01b5f5260045260245ffd5b80546001600160a01b031916968290036001600160a01b0316969096179095559293849390611391611377565b636c0d979760e11b5f5260045260245ffd5b9192909360a435936040840151804211610a9457506020845101518086116114545750918591610a7c61143194610a776020880151866119c4565b51516001600160a01b039081169260843591821682036102f057611391936118f9565b633728b83d60e01b5f5260045260245ffd5b90611470826110af565b61147d6040519182610f7d565b828152809261148e601f19916110af565b0190602036910137565b80516020909101905f5b8181106114af5750505090565b82518452602093840193909201916001016114a2565b6114cd611234565b9060405190602082019261190160f01b845260228301526042820152604281526112eb606282610f7d565b91908260409103126102f0576020823592013590565b833b61161157604182036115b157611528828201826114f8565b93909260401015611220576020935f9360ff6040608095013560f81c5b60405194855216868401526040830152606082015282805260015afa156115a6575f516001600160a01b0316908115611597576001600160a01b03160361158857565b632057875960e21b5f5260045ffd5b638baa579f60e01b5f5260045ffd5b6040513d5f823e3d90fd5b60408203611602576115c5918101906114f8565b6001600160ff1b0381169260ff91821c601b019182116115ee576020935f9360ff608094611545565b634e487b7160e01b5f52601160045260245ffd5b634be6321b60e01b5f5260045ffd5b928193606460209460405196879586948593630b135d3f60e11b8552600485015260406024850152816044850152848401375f828201840152601f01601f191681010301916001600160a01b03165afa9081156115a6575f91611698575b506001600160e01b0319166374eca2c160e11b0161168957565b632c19a72f60e21b5f5260045ffd5b90506020813d6020116116d3575b816116b360209383610f7d565b810103126102f057516001600160e01b0319811681036102f0575f61166f565b3d91506116a6565b606081015181516020808401516040948501516001600160a01b039687165f818152600185528781209589168082529585528781208a8a1682529094529590922080549690911696929565ffffffffffff94851694909216929160d01c8490036107f2577fc6a377bfc4eb120024a8ac08eef205be16b817020812c73223e81d1bdb9708ec936117cf91846117d4578865ffffffffffff42165b65ffffffffffff60a01b60a09190911b166001600160d01b03196001850160d01b1617179055604080516001600160a01b03998a16815265ffffffffffff9586166020820152949091169084015295169481906060820190565b0390a4565b8885611775565b6117e581516119fb565b90604060018060a01b036020830151169101516040519160208301937ff3841cd1ff0085026a6327b620b67997ce40f282c88a8e905a7a5626e310f3d08552604084015260608301526080820152608081526112eb60a082610f7d565b91959093825151956040840151804211610a9457508787036118ea5761187592610a7c8693610a776020880151866119c4565b5f5b84811061188657505050505050565b61189181835161120c565b51602061189f8389886112f1565b0135602082015180821161145457509081600193926118c1575b505001611877565b6118e391848060a01b03905116866118dd6104d2868d8c6112f1565b916118f9565b5f806118b9565b631fec674760e31b5f5260045ffd5b905f6064926020958295604051946323b872dd60e01b86526004860152602485015260448401525af13d15601f3d1160015f51141617161561193757565b60405162461bcd60e51b81526020600482015260146024820152731514905394d1915497d19493d357d1905253115160621b6044820152606490fd5b6040516020808201927f618358ac3db8dc274f0cd8829da7e234bd48cd73c4a740aede1adec9846d06a1845260018060a01b03815116604084015201516060820152606081526112eb608082610f7d565b90600160ff82161b9160018060a01b03165f525f60205260405f209060081c5f5260205260405f208181541880915516156107f257565b60405165ffffffffffff606060208301937f65626cad6cb96493bf6f5ebea28756c966f023ab9e8a83a7101849d5573b3678855260018060a01b03815116604085015260018060a01b036020820151168285015282604082015116608085015201511660a082015260a081526112eb60c082610f7d56fea2646970667358221220404c8b006a61fae10f2a751f31fdd45c5ff8d893b9f1b6f44323e7566a7418fa64736f6c634300081c0033";
        public Permit2DeploymentBase() : base(BYTECODE) { }
        public Permit2DeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class DomainSeparatorFunction : DomainSeparatorFunctionBase { }

    [Function("DOMAIN_SEPARATOR", "bytes32")]
    public class DomainSeparatorFunctionBase : FunctionMessage
    {

    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", typeof(AllowanceOutputDTO))]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint160", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint48", "expiration", 4)]
        public virtual ulong Expiration { get; set; }
    }

    public partial class HashPermitSingleFunction : HashPermitSingleFunctionBase { }

    [Function("hashPermitSingle", "bytes32")]
    public class HashPermitSingleFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "permitSingle", 1)]
        public virtual PermitSingle PermitSingle { get; set; }
    }

    public partial class InvalidateNoncesFunction : InvalidateNoncesFunctionBase { }

    [Function("invalidateNonces")]
    public class InvalidateNoncesFunctionBase : FunctionMessage
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint48", "newNonce", 3)]
        public virtual ulong NewNonce { get; set; }
    }

    public partial class InvalidateUnorderedNoncesFunction : InvalidateUnorderedNoncesFunctionBase { }

    [Function("invalidateUnorderedNonces")]
    public class InvalidateUnorderedNoncesFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "wordPos", 1)]
        public virtual BigInteger WordPos { get; set; }
        [Parameter("uint256", "mask", 2)]
        public virtual BigInteger Mask { get; set; }
    }

    public partial class LockdownFunction : LockdownFunctionBase { }

    [Function("lockdown")]
    public class LockdownFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "approvals", 1)]
        public virtual List<TokenSpenderPair> Approvals { get; set; }
    }

    public partial class NonceBitmapFunction : NonceBitmapFunctionBase { }

    [Function("nonceBitmap", "uint256")]
    public class NonceBitmapFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
    }

    public partial class PermitFunction : PermitFunctionBase { }

    [Function("permit")]
    public class PermitFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("tuple", "permitBatch", 2)]
        public virtual PermitBatch PermitBatch { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class Permit1Function : Permit1FunctionBase { }

    [Function("permit")]
    public class Permit1FunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("tuple", "permitSingle", 2)]
        public virtual PermitSingle PermitSingle { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PermitTransferFromFunction : PermitTransferFromFunctionBase { }

    [Function("permitTransferFrom")]
    public class PermitTransferFromFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "permit", 1)]
        public virtual PermitTransferFrom Permit { get; set; }
        [Parameter("tuple", "transferDetails", 2)]
        public virtual SignatureTransferDetails TransferDetails { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
        [Parameter("bytes", "signature", 4)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PermitTransferFrom1Function : PermitTransferFrom1FunctionBase { }

    [Function("permitTransferFrom")]
    public class PermitTransferFrom1FunctionBase : FunctionMessage
    {
        [Parameter("tuple", "permit", 1)]
        public virtual PermitBatchTransferFrom Permit { get; set; }
        [Parameter("tuple[]", "transferDetails", 2)]
        public virtual List<SignatureTransferDetails> TransferDetails { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
        [Parameter("bytes", "signature", 4)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PermitWitnessTransferFromFunction : PermitWitnessTransferFromFunctionBase { }

    [Function("permitWitnessTransferFrom")]
    public class PermitWitnessTransferFromFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "permit", 1)]
        public virtual PermitTransferFrom Permit { get; set; }
        [Parameter("tuple", "transferDetails", 2)]
        public virtual SignatureTransferDetails TransferDetails { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "witness", 4)]
        public virtual byte[] Witness { get; set; }
        [Parameter("string", "witnessTypeString", 5)]
        public virtual string WitnessTypeString { get; set; }
        [Parameter("bytes", "signature", 6)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class PermitWitnessTransferFrom1Function : PermitWitnessTransferFrom1FunctionBase { }

    [Function("permitWitnessTransferFrom")]
    public class PermitWitnessTransferFrom1FunctionBase : FunctionMessage
    {
        [Parameter("tuple", "permit", 1)]
        public virtual PermitBatchTransferFrom Permit { get; set; }
        [Parameter("tuple[]", "transferDetails", 2)]
        public virtual List<SignatureTransferDetails> TransferDetails { get; set; }
        [Parameter("address", "owner", 3)]
        public virtual string Owner { get; set; }
        [Parameter("bytes32", "witness", 4)]
        public virtual byte[] Witness { get; set; }
        [Parameter("string", "witnessTypeString", 5)]
        public virtual string WitnessTypeString { get; set; }
        [Parameter("bytes", "signature", 6)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "transferDetails", 1)]
        public virtual List<AllowanceTransferDetails> TransferDetails { get; set; }
    }

    public partial class TransferFrom1Function : TransferFrom1FunctionBase { }

    [Function("transferFrom")]
    public class TransferFrom1FunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint160", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("address", "token", 4)]
        public virtual string Token { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "token", 2, true )]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 3, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint160", "amount", 4, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint48", "expiration", 5, false )]
        public virtual ulong Expiration { get; set; }
    }

    public partial class LockdownEventDTO : LockdownEventDTOBase { }

    [Event("Lockdown")]
    public class LockdownEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "token", 2, false )]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 3, false )]
        public virtual string Spender { get; set; }
    }

    public partial class NonceInvalidationEventDTO : NonceInvalidationEventDTOBase { }

    [Event("NonceInvalidation")]
    public class NonceInvalidationEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "token", 2, true )]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 3, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint48", "newNonce", 4, false )]
        public virtual ulong NewNonce { get; set; }
        [Parameter("uint48", "oldNonce", 5, false )]
        public virtual ulong OldNonce { get; set; }
    }

    public partial class PermitEventDTO : PermitEventDTOBase { }

    [Event("Permit")]
    public class PermitEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "token", 2, true )]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 3, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint160", "amount", 4, false )]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint48", "expiration", 5, false )]
        public virtual ulong Expiration { get; set; }
        [Parameter("uint48", "nonce", 6, false )]
        public virtual ulong Nonce { get; set; }
    }

    public partial class UnorderedNonceInvalidationEventDTO : UnorderedNonceInvalidationEventDTOBase { }

    [Event("UnorderedNonceInvalidation")]
    public class UnorderedNonceInvalidationEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "word", 2, false )]
        public virtual BigInteger Word { get; set; }
        [Parameter("uint256", "mask", 3, false )]
        public virtual BigInteger Mask { get; set; }
    }

    public partial class AllowanceExpiredError : AllowanceExpiredErrorBase { }

    [Error("AllowanceExpired")]
    public class AllowanceExpiredErrorBase : IErrorDTO
    {
        [Parameter("uint256", "deadline", 1)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class ExcessiveInvalidationError : ExcessiveInvalidationErrorBase { }
    [Error("ExcessiveInvalidation")]
    public class ExcessiveInvalidationErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientAllowanceError : InsufficientAllowanceErrorBase { }

    [Error("InsufficientAllowance")]
    public class InsufficientAllowanceErrorBase : IErrorDTO
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class InvalidAmountError : InvalidAmountErrorBase { }

    [Error("InvalidAmount")]
    public class InvalidAmountErrorBase : IErrorDTO
    {
        [Parameter("uint256", "maxAmount", 1)]
        public virtual BigInteger MaxAmount { get; set; }
    }

    public partial class InvalidContractSignatureError : InvalidContractSignatureErrorBase { }
    [Error("InvalidContractSignature")]
    public class InvalidContractSignatureErrorBase : IErrorDTO
    {
    }

    public partial class InvalidNonceError : InvalidNonceErrorBase { }
    [Error("InvalidNonce")]
    public class InvalidNonceErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignatureError : InvalidSignatureErrorBase { }
    [Error("InvalidSignature")]
    public class InvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignatureLengthError : InvalidSignatureLengthErrorBase { }
    [Error("InvalidSignatureLength")]
    public class InvalidSignatureLengthErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignerError : InvalidSignerErrorBase { }
    [Error("InvalidSigner")]
    public class InvalidSignerErrorBase : IErrorDTO
    {
    }

    public partial class LengthMismatchError : LengthMismatchErrorBase { }
    [Error("LengthMismatch")]
    public class LengthMismatchErrorBase : IErrorDTO
    {
    }

    public partial class SignatureExpiredError : SignatureExpiredErrorBase { }

    [Error("SignatureExpired")]
    public class SignatureExpiredErrorBase : IErrorDTO
    {
        [Parameter("uint256", "signatureDeadline", 1)]
        public virtual BigInteger SignatureDeadline { get; set; }
    }

    public partial class DomainSeparatorOutputDTO : DomainSeparatorOutputDTOBase { }

    [FunctionOutput]
    public class DomainSeparatorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint160", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint48", "expiration", 2)]
        public virtual ulong Expiration { get; set; }
        [Parameter("uint48", "nonce", 3)]
        public virtual ulong Nonce { get; set; }
    }



    public partial class HashPermitSingleOutputDTO : HashPermitSingleOutputDTOBase { }

    [FunctionOutput]
    public class HashPermitSingleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }







    public partial class NonceBitmapOutputDTO : NonceBitmapOutputDTOBase { }

    [FunctionOutput]
    public class NonceBitmapOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
















}
