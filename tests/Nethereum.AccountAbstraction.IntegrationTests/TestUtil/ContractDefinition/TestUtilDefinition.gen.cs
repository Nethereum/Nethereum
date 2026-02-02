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
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestUtil.ContractDefinition
{


    public partial class TestUtilDeployment : TestUtilDeploymentBase
    {
        public TestUtilDeployment() : base(BYTECODE) { }
        public TestUtilDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestUtilDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "6080806040523460155761028c908161001a8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f3560e01c9081632d9bd99b14610197575063a124062e14610032575f80fd5b346101935760203660031901126101935760043567ffffffffffffffff8111610193578060040161012060031983360301126101935780356001600160a01b03811691908290036101935760c461008c6044850183610223565b908160405191823720936100ba6100a66064830185610223565b9081604051918237209360e4830190610223565b908160405191823720926040519560208701957f29a0bca4af4be3421398da00295e58e6d7de38cb492214754cb6a47507dd6f8e8752604088015260248301356060880152608087015260a0860152608481013560c086015260a481013560e08601520135610100840152610120830152610120825261014082019180831067ffffffffffffffff84111761017f5761018090836040526020845280518093816101608401528383015e5f82848301015261013f1992601f8019910116810103010190f35b634e487b7160e01b5f52604160045260245ffd5b5f80fd5b34610193576020366003190112610193576004359067ffffffffffffffff821161019357366023830112156101935781600401359167ffffffffffffffff83116101935736602484830101116101935760209260246101f692016101fc565b15158152f35b9060021161021e57356bffffffffffffffffffffffff1916613b8160f11b1490565b505f90565b903590601e1981360301821215610193570180359067ffffffffffffffff8211610193576020019181360383136101935756fea2646970667358221220d6dd6404dd6b4ae17d6ae8c1548b36f8f2bda1ad31c5b9efd8f9c6bce5aa95cc64736f6c634300081d0033";
        public TestUtilDeploymentBase() : base(BYTECODE) { }
        public TestUtilDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class EncodeUserOpFunction : EncodeUserOpFunctionBase { }

    [Function("encodeUserOp", "bytes")]
    public class EncodeUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "op", 1)]
        public virtual PackedUserOperation Op { get; set; }
    }

    public partial class IsEip7702InitCodeFunction : IsEip7702InitCodeFunctionBase { }

    [Function("isEip7702InitCode", "bool")]
    public class IsEip7702InitCodeFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "initCode", 1)]
        public virtual byte[] InitCode { get; set; }
    }

    public partial class EncodeUserOpOutputDTO : EncodeUserOpOutputDTOBase { }

    [FunctionOutput]
    public class EncodeUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class IsEip7702InitCodeOutputDTO : IsEip7702InitCodeOutputDTOBase { }

    [FunctionOutput]
    public class IsEip7702InitCodeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}
