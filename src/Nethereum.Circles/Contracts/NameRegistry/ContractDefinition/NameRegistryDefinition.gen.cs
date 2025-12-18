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

namespace Nethereum.Circles.Contracts.NameRegistry.ContractDefinition
{


    public partial class NameRegistryDeployment : NameRegistryDeploymentBase
    {
        public NameRegistryDeployment() : base(BYTECODE) { }
        public NameRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class NameRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608060405234801561000f575f80fd5b50604051611a9d380380611a9d83398101604081905261002e916100f6565b6001600160a01b03811661005c5760405163d82c8fc960e01b81526011600482015260240160405180910390fd5b5f80546001600160a01b0319166001600160a01b03831690811782556040805180820182526007815266436972636c657360c81b6020808301919091529284526003909252909120906100af90826101bb565b50604080518082018252600381526243524360e81b6020808301919091526001600160a01b0384165f908152600490915291909120906100ef90826101bb565b505061027a565b5f60208284031215610106575f80fd5b81516001600160a01b038116811461011c575f80fd5b9392505050565b634e487b7160e01b5f52604160045260245ffd5b600181811c9082168061014b57607f821691505b60208210810361016957634e487b7160e01b5f52602260045260245ffd5b50919050565b601f8211156101b657805f5260205f20601f840160051c810160208510156101945750805b601f840160051c820191505b818110156101b3575f81556001016101a0565b50505b505050565b81516001600160401b038111156101d4576101d4610123565b6101e8816101e28454610137565b8461016f565b602080601f83116001811461021b575f84156102045750858301515b5f19600386901b1c1916600185901b178555610272565b5f85815260208120601f198616915b828110156102495788860151825594840194600190910190840161022a565b508582101561026657878501515f19600388901b60f8161c191681555b505060018460011b0185555b505050505050565b611816806102875f395ff3fe608060405234801561000f575f80fd5b506004361061013d575f3560e01c80635dabbfa7116100b4578063c5a5688c11610079578063c5a5688c14610331578063d199889314610344578063dc03a0f314610357578063e27871dd1461036a578063e44b8c351461037d578063e9973f7c14610390575f80fd5b80635dabbfa7146102e0578063829c0fde146102f35780638fd4f71a146102fb57806398245b0c1461030e578063a86e35761461031e575f80fd5b8063365a86fc11610105578063365a86fc146102245780633857d9d71461024e5780633bb7b6c5146102635780634068e58d1461027657806348f813b5146102955780634a4b8ae3146102b8575f80fd5b8063013046ae1461014157806301984892146101865780631455d1e6146101a65780631e30397f146101cb57806328898d0a146101ee575b5f80fd5b61016961014f366004611371565b60016020525f90815260409020546001600160481b031681565b6040516001600160481b0390911681526020015b60405180910390f35b610199610194366004611371565b6103c2565b60405161017d9190611391565b6101996040518060400160405280600681526020016552696e67732d60d01b81525081565b6101de6101d936600461140b565b6104f2565b604051901515815260200161017d565b6102166101fc366004611371565b6001600160a01b03165f9081526005602052604090205490565b60405190815260200161017d565b5f54610236906001600160a01b031681565b6040516001600160a01b03909116815260200161017d565b61026161025c36600461144a565b61072a565b005b610199610271366004611371565b610737565b610216610284366004611371565b60056020525f908152604090205481565b6101996040518060400160405280600481526020016352494e4760e01b81525081565b6102366102c6366004611461565b60026020525f90815260409020546001600160a01b031681565b6101996102ee366004611371565b6107ce565b6102616107e6565b6101de61030936600461140b565b6107f0565b610169684e900abb53e6b70fff81565b61019961032c366004611371565b610946565b61026161033f36600461144a565b610b27565b610261610352366004611487565b610b30565b6101696103653660046114d6565b610bc6565b610261610378366004611487565b610c39565b61026161038b3660046114d6565b610cc8565b6103a361039e366004611371565b610d0e565b604080516001600160481b03909316835260208301919091520161017d565b5f5460405163f72c436f60e01b81526001600160a01b038381166004830152606092169063f72c436f90602401602060405180830381865afa15801561040a573d5f803e3d5ffd5b505050506040513d601f19601f8201168201806040525081019061042e91906114fe565b6104e3576001600160a01b0382165f90815260036020526040812080546104549061151d565b80601f01602080910402602001604051908101604052809291908181526020018280546104809061151d565b80156104cb5780601f106104a2576101008083540402835291602001916104cb565b820191905f5260205f20905b8154815290600101906020018083116104ae57829003601f168201915b505050505090505f815111156104e15792915050565b505b6104ec82610dc8565b92915050565b5f8083838080601f0160208091040260200160405190810160405280939291908181526020018383808284375f9201919091525050825192935050602090911190508061053e57508051155b1561054c575f9150506104ec565b5f5b815181101561071f575f82828151811061056a5761056a611555565b01602001516001600160f81b0319169050600360fc1b811080159061059d5750603960f81b6001600160f81b0319821611155b1580156105d35750604160f81b6001600160f81b03198216108015906105d15750602d60f91b6001600160f81b0319821611155b155b80156106085750606160f81b6001600160f81b03198216108015906106065750603d60f91b6001600160f81b0319821611155b155b80156106225750600160fd1b6001600160f81b0319821614155b80156106545750602d60f81b6001600160f81b0319821614806106525750605f60f81b6001600160f81b03198216145b155b801561066e5750601760f91b6001600160f81b0319821614155b80156106a05750600560fb1b6001600160f81b03198216148061069e5750602960f81b6001600160f81b03198216145b155b80156106ba5750602760f81b6001600160f81b0319821614155b80156106d45750601360f91b6001600160f81b0319821614155b80156107065750602b60f81b6001600160f81b0319821614806107045750602360f81b6001600160f81b03198216145b155b15610716575f93505050506104ec565b5060010161054e565b506001949350505050565b6107343382610e62565b50565b60046020525f90815260409020805461074f9061151d565b80601f016020809104026020016040519081016040528092919081815260200182805461077b9061151d565b80156107c65780601f1061079d576101008083540402835291602001916107c6565b820191905f5260205f20905b8154815290600101906020018083116107a957829003601f168201915b505050505081565b60036020525f90815260409020805461074f9061151d565b6107ee610eba565b565b5f8083838080601f0160208091040260200160405190810160405280939291908181526020018383808284375f9201919091525050825192935050901590508061083b575060108151115b15610849575f9150506104ec565b5f5b815181101561071f575f82828151811061086757610867611555565b01602001516001600160f81b0319169050600360fc1b811080159061089a5750603960f81b6001600160f81b0319821611155b806108cc5750604160f81b6001600160f81b03198216108015906108cc5750602d60f91b6001600160f81b0319821611155b806108fe5750606160f81b6001600160f81b03198216108015906108fe5750603d60f91b6001600160f81b0319821611155b806109165750602d60f81b6001600160f81b03198216145b8061092e5750605f60f81b6001600160f81b03198216145b61093d575f93505050506104ec565b5060010161084b565b5f5460405163b1ce8eab60e01b81526001600160a01b038381166004830152606092169063b1ce8eab90602401602060405180830381865afa15801561098e573d5f803e3d5ffd5b505050506040513d601f19601f820116820180604052508101906109b291906114fe565b156109e657604051633191a38d60e01b81526001600160a01b03831660048201525f60248201526044015b60405180910390fd5b5f5460405163278330f160e21b81526001600160a01b03848116600483015290911690639e0cc3c490602401602060405180830381865afa158015610a2d573d5f803e3d5ffd5b505050506040513d601f19601f82011682018060405250810190610a5191906114fe565b15610b07576001600160a01b0382165f9081526004602052604081208054610a789061151d565b80601f0160208091040260200160405190810160405280929190818152602001828054610aa49061151d565b8015610aef5780601f10610ac657610100808354040283529160200191610aef565b820191905f5260205f20905b815481529060010190602001808311610ad257829003601f168201915b505050505090505f81511115610b055792915050565b505b505060408051808201909152600481526352494e4760e01b602082015290565b61073481610ed8565b5f5460e6906001600160a01b03163314610b675760405162c14c0760e81b815233600482015260ff821660248201526044016109dd565b8115610bc057610b7783836104f2565b610b9c578383835f60405163d76958f760e01b81526004016109dd9493929190611591565b6001600160a01b0384165f908152600360205260409020610bbe838583611621565b505b50505050565b6040516bffffffffffffffffffffffff19606084901b166020820152603481018290525f908190605401604051602081830303815290604052805190602001209050684e900abb53e6b70fff6001610c1e91906116ef565b610c31906001600160481b03168261172a565b949350505050565b5f5460e7906001600160a01b03163314610c705760405162c14c0760e81b815233600482015260ff821660248201526044016109dd565b8115610bc057610c8083836107f0565b610ca657838383600160405163d76958f760e01b81526004016109dd9493929190611591565b6001600160a01b0384165f908152600460205260409020610bbe838583611621565b5f5460e5906001600160a01b03163314610cff5760405162c14c0760e81b815233600482015260ff821660248201526044016109dd565b610d098383610e62565b505050565b6001600160a01b0381165f9081526001602052604081205481906001600160481b031615610d7e576001600160a01b0383165f8181526001602052604080822054905163ec7765c960e01b815260048101939093526001600160481b0316602483015260448201526064016109dd565b610d888382610bc6565b6001600160481b0381165f908152600260205260409020549092506001600160a01b031615610dc35780610dbb8161173d565b915050610d7e565b915091565b6001600160a01b0381165f908152600160205260409020546060906001600160481b031680610e4f575f610e04846001600160a01b0316610fc9565b90506040518060400160405280600681526020016552696e67732d60d01b81525081604051602001610e3792919061176c565b60405160208183030381529060405292505050919050565b5f610e04826001600160481b0316611094565b6001600160a01b0382165f8181526005602052604090819020839055517f0a1d44830b9ad1708d85ea4071d97fd532b52504d7397d3e44461badd9f4f82790610eae9084815260200190565b60405180910390a25050565b5f80610ec533610d0e565b91509150610ed43383836111d2565b5050565b335f908152600160205260409020546001600160481b031615610f3957335f8181526001602081905260409182902054915163ec7765c960e01b815260048101939093526001600160481b03909116602483015260448201526064016109dd565b5f610f443383610bc6565b6001600160481b0381165f908152600260205260409020549091506001600160a01b031615610fbe576001600160481b0381165f818152600260205260409081902054905163690a563f60e11b81523360048201526024810185905260448101929092526001600160a01b031660648201526084016109dd565b610ed43382846111d2565b6040805181815260608181018352915f91906020820181803683370190505090505f5b5f841180610ff8575080155b1561108a575f611009603a8661172a565b90506040518060600160405280603a81526020016117a7603a9139818151811061103557611035611555565b01602001516001600160f81b031916838361104f8161173d565b94508151811061106157611061611555565b60200101906001600160f81b03191690815f1a905350611082603a86611780565b945050610fec565b610c318282611299565b60408051600c8082528183019092526060915f91906020820181803683370190505090505f5b5f8411806110c6575080155b15611158575f6110d7603a8661172a565b90506040518060600160405280603a81526020016117a7603a9139818151811061110357611103611555565b01602001516001600160f81b031916838361111d8161173d565b94508151811061112f5761112f611555565b60200101906001600160f81b03191690815f1a905350611150603a86611780565b9450506110ba565b600c81101561108a576040518060600160405280603a81526020016117a7603a91395f8151811061118b5761118b611555565b01602001516001600160f81b03191682826111a58161173d565b9350815181106111b7576111b7611555565b60200101906001600160f81b03191690815f1a905350611158565b6001600160481b03821661120b576040516353a7307b60e01b81526001600160a01b0384166004820152602481018290526044016109dd565b6001600160a01b0383165f818152600160209081526040808320805468ffffffffffffffffff19166001600160481b038816908117909155808452600283529281902080546001600160a01b0319168517905580519283529082018490527f368e444a05faec665f223aebb06d81a86daf0bc59bd7bccc160042422c8b6229910160405180910390a2505050565b60605f8267ffffffffffffffff8111156112b5576112b56115c9565b6040519080825280601f01601f1916602001820160405280156112df576020820181803683370190505b5090505f5b8381101561134e5784816112f9600187611793565b6113039190611793565b8151811061131357611313611555565b602001015160f81c60f81b82828151811061133057611330611555565b60200101906001600160f81b03191690815f1a9053506001016112e4565b509392505050565b80356001600160a01b038116811461136c575f80fd5b919050565b5f60208284031215611381575f80fd5b61138a82611356565b9392505050565b602081525f82518060208401528060208501604085015e5f604082850101526040601f19601f83011684010191505092915050565b5f8083601f8401126113d6575f80fd5b50813567ffffffffffffffff8111156113ed575f80fd5b602083019150836020828501011115611404575f80fd5b9250929050565b5f806020838503121561141c575f80fd5b823567ffffffffffffffff811115611432575f80fd5b61143e858286016113c6565b90969095509350505050565b5f6020828403121561145a575f80fd5b5035919050565b5f60208284031215611471575f80fd5b81356001600160481b038116811461138a575f80fd5b5f805f60408486031215611499575f80fd5b6114a284611356565b9250602084013567ffffffffffffffff8111156114bd575f80fd5b6114c9868287016113c6565b9497909650939450505050565b5f80604083850312156114e7575f80fd5b6114f083611356565b946020939093013593505050565b5f6020828403121561150e575f80fd5b8151801515811461138a575f80fd5b600181811c9082168061153157607f821691505b60208210810361154f57634e487b7160e01b5f52602260045260245ffd5b50919050565b634e487b7160e01b5f52603260045260245ffd5b81835281816020850137505f828201602090810191909152601f909101601f19169091010190565b6001600160a01b03851681526060602082018190525f906115b59083018587611569565b905060ff8316604083015295945050505050565b634e487b7160e01b5f52604160045260245ffd5b601f821115610d0957805f5260205f20601f840160051c810160208510156116025750805b601f840160051c820191505b81811015610bbe575f815560010161160e565b67ffffffffffffffff831115611639576116396115c9565b61164d83611647835461151d565b836115dd565b5f601f84116001811461167e575f85156116675750838201355b5f19600387901b1c1916600186901b178355610bbe565b5f83815260208120601f198716915b828110156116ad578685013582556020948501946001909201910161168d565b50868210156116c9575f1960f88860031b161c19848701351681555b505060018560011b0183555050505050565b634e487b7160e01b5f52601160045260245ffd5b6001600160481b0381811683821601908082111561170f5761170f6116db565b5092915050565b634e487b7160e01b5f52601260045260245ffd5b5f8261173857611738611716565b500690565b5f6001820161174e5761174e6116db565b5060010190565b5f81518060208401855e5f93019283525090919050565b5f610c3161177a8386611755565b84611755565b5f8261178e5761178e611716565b500490565b818103818111156104ec576104ec6116db56fe31323334353637383941424344454647484a4b4c4d4e505152535455565758595a6162636465666768696a6b6d6e6f707172737475767778797aa2646970667358221220236fd6b34055d8cd7dbe598365c4100df254a7609fda2c654ef138125257d6eb64736f6c63430008190033";
        public NameRegistryDeploymentBase() : base(BYTECODE) { }
        public NameRegistryDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_hub", 1)]
        public virtual string Hub { get; set; }
    }

    public partial class DefaultCirclesNamePrefixFunction : DefaultCirclesNamePrefixFunctionBase { }

    [Function("DEFAULT_CIRCLES_NAME_PREFIX", "string")]
    public class DefaultCirclesNamePrefixFunctionBase : FunctionMessage
    {

    }

    public partial class DefaultCirclesSymbolFunction : DefaultCirclesSymbolFunctionBase { }

    [Function("DEFAULT_CIRCLES_SYMBOL", "string")]
    public class DefaultCirclesSymbolFunctionBase : FunctionMessage
    {

    }

    public partial class MaxShortNameFunction : MaxShortNameFunctionBase { }

    [Function("MAX_SHORT_NAME", "uint72")]
    public class MaxShortNameFunctionBase : FunctionMessage
    {

    }

    public partial class AvatarToMetaDataDigestFunction : AvatarToMetaDataDigestFunctionBase { }

    [Function("avatarToMetaDataDigest", "bytes32")]
    public class AvatarToMetaDataDigestFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CalculateShortNameWithNonceFunction : CalculateShortNameWithNonceFunctionBase { }

    [Function("calculateShortNameWithNonce", "uint72")]
    public class CalculateShortNameWithNonceFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("uint256", "_nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
    }

    public partial class CustomNamesFunction : CustomNamesFunctionBase { }

    [Function("customNames", "string")]
    public class CustomNamesFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CustomSymbolsFunction : CustomSymbolsFunctionBase { }

    [Function("customSymbols", "string")]
    public class CustomSymbolsFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetMetadataDigestFunction : GetMetadataDigestFunctionBase { }

    [Function("getMetadataDigest", "bytes32")]
    public class GetMetadataDigestFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class HubFunction : HubFunctionBase { }

    [Function("hub", "address")]
    public class HubFunctionBase : FunctionMessage
    {

    }

    public partial class IsValidNameFunction : IsValidNameFunctionBase { }

    [Function("isValidName", "bool")]
    public class IsValidNameFunctionBase : FunctionMessage
    {
        [Parameter("string", "_name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class IsValidSymbolFunction : IsValidSymbolFunctionBase { }

    [Function("isValidSymbol", "bool")]
    public class IsValidSymbolFunctionBase : FunctionMessage
    {
        [Parameter("string", "_symbol", 1)]
        public virtual string Symbol { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class RegisterCustomNameFunction : RegisterCustomNameFunctionBase { }

    [Function("registerCustomName")]
    public class RegisterCustomNameFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("string", "_name", 2)]
        public virtual string Name { get; set; }
    }

    public partial class RegisterCustomSymbolFunction : RegisterCustomSymbolFunctionBase { }

    [Function("registerCustomSymbol")]
    public class RegisterCustomSymbolFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("string", "_symbol", 2)]
        public virtual string Symbol { get; set; }
    }

    public partial class RegisterShortNameFunction : RegisterShortNameFunctionBase { }

    [Function("registerShortName")]
    public class RegisterShortNameFunctionBase : FunctionMessage
    {

    }

    public partial class RegisterShortNameWithNonceFunction : RegisterShortNameWithNonceFunctionBase { }

    [Function("registerShortNameWithNonce")]
    public class RegisterShortNameWithNonceFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_nonce", 1)]
        public virtual BigInteger Nonce { get; set; }
    }

    public partial class SearchShortNameFunction : SearchShortNameFunctionBase { }

    [Function("searchShortName", typeof(SearchShortNameOutputDTO))]
    public class SearchShortNameFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class SetMetadataDigestFunction : SetMetadataDigestFunctionBase { }

    [Function("setMetadataDigest")]
    public class SetMetadataDigestFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("bytes32", "_metadataDigest", 2)]
        public virtual byte[] MetadataDigest { get; set; }
    }

    public partial class ShortNameToAvatarFunction : ShortNameToAvatarFunctionBase { }

    [Function("shortNameToAvatar", "address")]
    public class ShortNameToAvatarFunctionBase : FunctionMessage
    {
        [Parameter("uint72", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ShortNamesFunction : ShortNamesFunctionBase { }

    [Function("shortNames", "uint72")]
    public class ShortNamesFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {
        [Parameter("address", "_avatar", 1)]
        public virtual string Avatar { get; set; }
    }

    public partial class UpdateMetadataDigestFunction : UpdateMetadataDigestFunctionBase { }

    [Function("updateMetadataDigest")]
    public class UpdateMetadataDigestFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "_metadataDigest", 1)]
        public virtual byte[] MetadataDigest { get; set; }
    }

    public partial class RegisterShortNameEventDTO : RegisterShortNameEventDTOBase { }

    [Event("RegisterShortName")]
    public class RegisterShortNameEventDTOBase : IEventDTO
    {
        [Parameter("address", "avatar", 1, true )]
        public virtual string Avatar { get; set; }
        [Parameter("uint72", "shortName", 2, false )]
        public virtual BigInteger ShortName { get; set; }
        [Parameter("uint256", "nonce", 3, false )]
        public virtual BigInteger Nonce { get; set; }
    }

    public partial class UpdateMetadataDigestEventDTO : UpdateMetadataDigestEventDTOBase { }

    [Event("UpdateMetadataDigest")]
    public class UpdateMetadataDigestEventDTOBase : IEventDTO
    {
        [Parameter("address", "avatar", 1, true )]
        public virtual string Avatar { get; set; }
        [Parameter("bytes32", "metadataDigest", 2, false )]
        public virtual byte[] MetadataDigest { get; set; }
    }

    public partial class CirclesAmountOverflowError : CirclesAmountOverflowErrorBase { }

    [Error("CirclesAmountOverflow")]
    public class CirclesAmountOverflowErrorBase : IErrorDTO
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesErrorAddressUintArgsError : CirclesErrorAddressUintArgsErrorBase { }

    [Error("CirclesErrorAddressUintArgs")]
    public class CirclesErrorAddressUintArgsErrorBase : IErrorDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("uint8", "", 3)]
        public virtual byte ReturnValue3 { get; set; }
    }

    public partial class CirclesErrorNoArgsError : CirclesErrorNoArgsErrorBase { }

    [Error("CirclesErrorNoArgs")]
    public class CirclesErrorNoArgsErrorBase : IErrorDTO
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class CirclesErrorOneAddressArgError : CirclesErrorOneAddressArgErrorBase { }

    [Error("CirclesErrorOneAddressArg")]
    public class CirclesErrorOneAddressArgErrorBase : IErrorDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint8", "", 2)]
        public virtual byte ReturnValue2 { get; set; }
    }

    public partial class CirclesIdMustBeDerivedFromAddressError : CirclesIdMustBeDerivedFromAddressErrorBase { }

    [Error("CirclesIdMustBeDerivedFromAddress")]
    public class CirclesIdMustBeDerivedFromAddressErrorBase : IErrorDTO
    {
        [Parameter("uint256", "providedId", 1)]
        public virtual BigInteger ProvidedId { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesInvalidCirclesIdError : CirclesInvalidCirclesIdErrorBase { }

    [Error("CirclesInvalidCirclesId")]
    public class CirclesInvalidCirclesIdErrorBase : IErrorDTO
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesInvalidParameterError : CirclesInvalidParameterErrorBase { }

    [Error("CirclesInvalidParameter")]
    public class CirclesInvalidParameterErrorBase : IErrorDTO
    {
        [Parameter("uint256", "parameter", 1)]
        public virtual BigInteger Parameter { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesNamesAvatarAlreadyHasCustomNameOrSymbolError : CirclesNamesAvatarAlreadyHasCustomNameOrSymbolErrorBase { }

    [Error("CirclesNamesAvatarAlreadyHasCustomNameOrSymbol")]
    public class CirclesNamesAvatarAlreadyHasCustomNameOrSymbolErrorBase : IErrorDTO
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("string", "nameOrSymbol", 2)]
        public virtual string NameOrSymbol { get; set; }
        [Parameter("uint8", "code", 3)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesNamesInvalidNameError : CirclesNamesInvalidNameErrorBase { }

    [Error("CirclesNamesInvalidName")]
    public class CirclesNamesInvalidNameErrorBase : IErrorDTO
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("string", "name", 2)]
        public virtual string Name { get; set; }
        [Parameter("uint8", "code", 3)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesNamesOrganizationHasNoSymbolError : CirclesNamesOrganizationHasNoSymbolErrorBase { }

    [Error("CirclesNamesOrganizationHasNoSymbol")]
    public class CirclesNamesOrganizationHasNoSymbolErrorBase : IErrorDTO
    {
        [Parameter("address", "organization", 1)]
        public virtual string Organization { get; set; }
        [Parameter("uint8", "code", 2)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesNamesShortNameAlreadyAssignedError : CirclesNamesShortNameAlreadyAssignedErrorBase { }

    [Error("CirclesNamesShortNameAlreadyAssigned")]
    public class CirclesNamesShortNameAlreadyAssignedErrorBase : IErrorDTO
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("uint72", "shortName", 2)]
        public virtual BigInteger ShortName { get; set; }
        [Parameter("uint8", "code", 3)]
        public virtual byte Code { get; set; }
    }

    public partial class CirclesNamesShortNameWithNonceTakenError : CirclesNamesShortNameWithNonceTakenErrorBase { }

    [Error("CirclesNamesShortNameWithNonceTaken")]
    public class CirclesNamesShortNameWithNonceTakenErrorBase : IErrorDTO
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("uint256", "nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("uint72", "shortName", 3)]
        public virtual BigInteger ShortName { get; set; }
        [Parameter("address", "takenByAvatar", 4)]
        public virtual string TakenByAvatar { get; set; }
    }

    public partial class CirclesNamesShortNameZeroError : CirclesNamesShortNameZeroErrorBase { }

    [Error("CirclesNamesShortNameZero")]
    public class CirclesNamesShortNameZeroErrorBase : IErrorDTO
    {
        [Parameter("address", "avatar", 1)]
        public virtual string Avatar { get; set; }
        [Parameter("uint256", "nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
    }

    public partial class CirclesProxyAlreadyInitializedError : CirclesProxyAlreadyInitializedErrorBase { }
    [Error("CirclesProxyAlreadyInitialized")]
    public class CirclesProxyAlreadyInitializedErrorBase : IErrorDTO
    {
    }

    public partial class CirclesReentrancyGuardError : CirclesReentrancyGuardErrorBase { }

    [Error("CirclesReentrancyGuard")]
    public class CirclesReentrancyGuardErrorBase : IErrorDTO
    {
        [Parameter("uint8", "code", 1)]
        public virtual byte Code { get; set; }
    }

    public partial class DefaultCirclesNamePrefixOutputDTO : DefaultCirclesNamePrefixOutputDTOBase { }

    [FunctionOutput]
    public class DefaultCirclesNamePrefixOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class DefaultCirclesSymbolOutputDTO : DefaultCirclesSymbolOutputDTOBase { }

    [FunctionOutput]
    public class DefaultCirclesSymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class MaxShortNameOutputDTO : MaxShortNameOutputDTOBase { }

    [FunctionOutput]
    public class MaxShortNameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint72", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AvatarToMetaDataDigestOutputDTO : AvatarToMetaDataDigestOutputDTOBase { }

    [FunctionOutput]
    public class AvatarToMetaDataDigestOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class CalculateShortNameWithNonceOutputDTO : CalculateShortNameWithNonceOutputDTOBase { }

    [FunctionOutput]
    public class CalculateShortNameWithNonceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint72", "shortName_", 1)]
        public virtual BigInteger Shortname { get; set; }
    }

    public partial class CustomNamesOutputDTO : CustomNamesOutputDTOBase { }

    [FunctionOutput]
    public class CustomNamesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CustomSymbolsOutputDTO : CustomSymbolsOutputDTOBase { }

    [FunctionOutput]
    public class CustomSymbolsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetMetadataDigestOutputDTO : GetMetadataDigestOutputDTOBase { }

    [FunctionOutput]
    public class GetMetadataDigestOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class HubOutputDTO : HubOutputDTOBase { }

    [FunctionOutput]
    public class HubOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsValidNameOutputDTO : IsValidNameOutputDTOBase { }

    [FunctionOutput]
    public class IsValidNameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidSymbolOutputDTO : IsValidSymbolOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }









    public partial class SearchShortNameOutputDTO : SearchShortNameOutputDTOBase { }

    [FunctionOutput]
    public class SearchShortNameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint72", "shortName_", 1)]
        public virtual BigInteger Shortname { get; set; }
        [Parameter("uint256", "nonce_", 2)]
        public virtual BigInteger Nonce { get; set; }
    }



    public partial class ShortNameToAvatarOutputDTO : ShortNameToAvatarOutputDTOBase { }

    [FunctionOutput]
    public class ShortNameToAvatarOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ShortNamesOutputDTO : ShortNamesOutputDTOBase { }

    [FunctionOutput]
    public class ShortNamesOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint72", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }


}
