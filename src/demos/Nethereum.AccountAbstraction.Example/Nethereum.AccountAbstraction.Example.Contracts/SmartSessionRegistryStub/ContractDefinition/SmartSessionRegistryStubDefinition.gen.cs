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
using Nethereum.AccountAbstraction.Example.Contracts.SmartSessionRegistryStub.ContractDefinition;

namespace Nethereum.AccountAbstraction.Example.Contracts.SmartSessionRegistryStub.ContractDefinition
{


    public partial class SmartSessionRegistryStubDeployment : SmartSessionRegistryStubDeploymentBase
    {
        public SmartSessionRegistryStubDeployment() : base(BYTECODE) { }
        public SmartSessionRegistryStubDeployment(string byteCode) : base(byteCode) { }
    }

    public class SmartSessionRegistryStubDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080806040523460155761021d908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c80630bb30abc146101505780632ed94467146101165780634c13560c146100fd578063529562a1146100db57806396fb7217146100c2578063c23697a8146100a95763f05c04e114610066575f80fd5b346100a55760403660031901126100a55760043560ff8116036100a55760243567ffffffffffffffff81116100a5576100a39036906004016101b6565b005b5f80fd5b346100a55760203660031901126100a5576100a361018a565b346100a55760403660031901126100a5576100a361018a565b346100a55760603660031901126100a5576100f461018a565b506100a36101a0565b346100a55760403660031901126100a5576100f461018a565b346100a55760803660031901126100a55761012f61018a565b5060443567ffffffffffffffff81116100a5576100a39036906004016101b6565b346100a55760603660031901126100a55761016961018a565b5060243567ffffffffffffffff81116100a5576100a39036906004016101b6565b600435906001600160a01b03821682036100a557565b602435906001600160a01b03821682036100a557565b9181601f840112156100a55782359167ffffffffffffffff83116100a5576020808501948460051b0101116100a55756fea26469706673582212204424d6f5d74fc7eac6282ea91eedebdb42909f13bfc209af352217af4c59289364736f6c634300081c0033";
        public SmartSessionRegistryStubDeploymentBase() : base(BYTECODE) { }
        public SmartSessionRegistryStubDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class Check2Function : Check2FunctionBase { }

    [Function("check")]
    public class Check2FunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address[]", "", 2)]
        public virtual List<string> ReturnValue2 { get; set; }
        [Parameter("uint256", "", 3)]
        public virtual BigInteger ReturnValue3 { get; set; }
    }

    public partial class Check3Function : Check3FunctionBase { }

    [Function("check")]
    public class Check3FunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("address[]", "", 3)]
        public virtual List<string> ReturnValue3 { get; set; }
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
    }

    public partial class Check1Function : Check1FunctionBase { }

    [Function("check")]
    public class Check1FunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
    }

    public partial class CheckFunction : CheckFunctionBase { }

    [Function("check")]
    public class CheckFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CheckForAccountFunction : CheckForAccountFunctionBase { }

    [Function("checkForAccount")]
    public class CheckForAccountFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
    }

    public partial class CheckForAccount1Function : CheckForAccount1FunctionBase { }

    [Function("checkForAccount")]
    public class CheckForAccount1FunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("uint256", "", 3)]
        public virtual BigInteger ReturnValue3 { get; set; }
    }

    public partial class TrustAttestersFunction : TrustAttestersFunctionBase { }

    [Function("trustAttesters")]
    public class TrustAttestersFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
        [Parameter("address[]", "", 2)]
        public virtual List<string> ReturnValue2 { get; set; }
    }














}
