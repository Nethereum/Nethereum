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
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition
{


    public partial class ValueCapCombinatorDeployment : ValueCapCombinatorDeploymentBase
    {
        public ValueCapCombinatorDeployment() : base(BYTECODE) { }
        public ValueCapCombinatorDeployment(string byteCode) : base(byteCode) { }
    }

    public class ValueCapCombinatorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a034606d57601f61066638819003918201601f19168301916001600160401b03831184841017607157808492602094604052833981010312606d57516001600160a01b0381168103606d576080526040516105e09081610086823960805181818161012101526102a80152f35b5f80fd5b634e487b7160e01b5f52604160045260245ffdfe6080806040526004361015610012575f80fd5b5f3560e01c90816301ffc9a7146104c35750806305c00895146102d75780637b10399914610293578063989c9e46146100a95763ac5dadbd14610053575f80fd5b346100a55760403660031901126100a55761006c61052e565b6004355f9081526020818152604080832033845282528083206001600160a01b0394851684528252918290205491519190921615158152f35b5f80fd5b346100a55760603660031901126100a5576004356001600160a01b038116908190036100a5576024356044359167ffffffffffffffff83116100a5576100f5604093369060040161055a565b90809491810103126100a5576040516375f5632d60e01b815283356004820181905293906020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa908115610288575f91610246575b506001600160a01b0316938415610234575060405190604082019082821067ffffffffffffffff831117610220577f5d14f8bf6f75758495bb0b0768b81cdebc7869d1f19edacc2f483ca0c89a171595600192604052835260208084019101358152845f525f60205260405f20828060a01b0333165f5260205260405f20845f5260205260405f2092828060a01b039051166bffffffffffffffffffffffff60a01b84541617835551910155604051918252336020830152604082015260608180030190a1005b634e487b7160e01b5f52604160045260245ffd5b6353d07feb60e01b5f5260045260245ffd5b90506020813d602011610280575b8161026160209383610588565b810103126100a557516001600160a01b03811681036100a55785610159565b3d9150610254565b6040513d5f823e3d90fd5b346100a5575f3660031901126100a5576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b346100a55760a03660031901126100a5576004356102f361052e565b6102fb610544565b5060843567ffffffffffffffff81116100a55761031c90369060040161055a565b50505f8281526020818152604080832033845282528083206001600160a01b038581168552925290912080549091169290919083156104a25760445f8560018601549060405160208101926064358452604082015260408152610380606082610588565b604051948580948193631658af3760e01b8352602060048401525180918160248501528484015e8181018301879052601f01601f191681010301915afa908115610288575f91610418575b506020815103610409576020818051810103126100a5576020015180151581036100a557156104005760205f5b604051908152f35b602060016103f8565b63cc7a047360e01b5f5260045ffd5b90503d805f833e6104298183610588565b8101906020818303126100a55780519067ffffffffffffffff82116100a5570181601f820112156100a55780519067ffffffffffffffff8211610220576040519261047e601f8401601f191660200185610588565b828452602083830101116100a557815f9260208093018386015e83010152816103cb565b63721f9ead60e11b5f526004523360245260018060a01b031660445260645ffd5b346100a55760203660031901126100a5576004359063ffffffff60e01b82168092036100a5576020916301ffc9a760e01b811490811561051d575b811561050c575b5015158152f35b6305c0089560e01b14905083610505565b634c4e4f2360e11b811491506104fe565b602435906001600160a01b03821682036100a557565b604435906001600160a01b03821682036100a557565b9181601f840112156100a55782359167ffffffffffffffff83116100a557602083818601950101116100a557565b90601f8019910116810190811067ffffffffffffffff8211176102205760405256fea26469706673582212206f943b721b247890ec9d709d109346d213313f62c24f282aaf7073fcc8601f8364736f6c634300081c0033";
        public ValueCapCombinatorDeploymentBase() : base(BYTECODE) { }
        public ValueCapCombinatorDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_registry", 1)]
        public virtual string Registry { get; set; }
    }

    public partial class CheckActionFunction : CheckActionFunctionBase { }

    [Function("checkAction", "uint256")]
    public class CheckActionFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
        [Parameter("uint256", "value", 4)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class InitializeWithMultiplexerFunction : InitializeWithMultiplexerFunctionBase { }

    [Function("initializeWithMultiplexer")]
    public class InitializeWithMultiplexerFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "configId", 2)]
        public virtual byte[] ConfigId { get; set; }
        [Parameter("bytes", "initData", 3)]
        public virtual byte[] InitData { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class RegistryFunction : RegistryFunctionBase { }

    [Function("registry", "address")]
    public class RegistryFunctionBase : FunctionMessage
    {

    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceID", 1)]
        public virtual byte[] InterfaceID { get; set; }
    }

    public partial class CheckActionOutputDTO : CheckActionOutputDTOBase { }

    [FunctionOutput]
    public class CheckActionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class IsInitializedOutputDTO : IsInitializedOutputDTOBase { }

    [FunctionOutput]
    public class IsInitializedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class RegistryOutputDTO : RegistryOutputDTOBase { }

    [FunctionOutput]
    public class RegistryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class PolicySetEventDTO : PolicySetEventDTOBase { }

    [Event("PolicySet")]
    public class PolicySetEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "id", 1, false )]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "multiplexer", 2, false )]
        public virtual string Multiplexer { get; set; }
        [Parameter("address", "account", 3, false )]
        public virtual string Account { get; set; }
    }

    public partial class InvalidRuleOutputError : InvalidRuleOutputErrorBase { }
    [Error("InvalidRuleOutput")]
    public class InvalidRuleOutputErrorBase : IErrorDTO
    {
    }

    public partial class NotInitializedError : NotInitializedErrorBase { }

    [Error("NotInitialized")]
    public class NotInitializedErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "id", 1)]
        public virtual byte[] Id { get; set; }
        [Parameter("address", "mxer", 2)]
        public virtual string Mxer { get; set; }
        [Parameter("address", "account", 3)]
        public virtual string Account { get; set; }
    }

    public partial class UnknownRuleError : UnknownRuleErrorBase { }

    [Error("UnknownRule")]
    public class UnknownRuleErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "ruleId", 1)]
        public virtual byte[] RuleId { get; set; }
    }
}
