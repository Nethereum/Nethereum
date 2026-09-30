using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestUtil.ContractDefinition
{


    public partial class TestUtilDeployment : TestUtilDeploymentBase
    {
        public TestUtilDeployment() : base(BYTECODE) { }
        public TestUtilDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestUtilDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608080604052346015576105ff908161001a8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f3560e01c9081632d9bd99b14610234575063a124062e14610032575f80fd5b346102305760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126102305760043567ffffffffffffffff81116102305780600401906101207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc82360301126102305781359173ffffffffffffffffffffffffffffffffffffffff83168093036102305760c46100d5604484018361030c565b9081604051918237209261010c6101066100f2606484018661030c565b9081604051918237209460e484019061030c565b9061035d565b926040519460208601967f29a0bca4af4be3421398da00295e58e6d7de38cb492214754cb6a47507dd6f8e8852604087015260248301356060870152608086015260a0850152608481013560c085015260a481013560e08501520135610100830152610120820152610120815261014081019181831067ffffffffffffffff841117610203577ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffec0917fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f61018093866040526020875283518091816101608701528686015e5f8582860101520116810103010190f35b7f4e487b71000000000000000000000000000000000000000000000000000000005f52604160045260245ffd5b5f80fd5b346102305760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610230576004359067ffffffffffffffff821161023057366023830112156102305781600401359167ffffffffffffffff83116102305736602484830101116102305760209260246102b192016102b7565b15158152f35b9060021161030757357fffffffffffffffffffffffffffffffffffffffff000000000000000000000000167f77020000000000000000000000000000000000000000000000000000000000001490565b505f90565b9035907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe181360301821215610230570180359067ffffffffffffffff82116102305760200191813603831361023057565b610367828261042b565b806103785750816040519182372090565b7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe919203604051927ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff682019084377f22e325a2974396560000000000000000000000000000000000000000000000007ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff6828501015201902090565b90939293848311610230578411610230578101920390565b603e82106105ae577f22e325a2974396560000000000000000000000000000000000000000000000007fffffffffffffffff0000000000000000000000000000000000000000000000006104a3847ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff881018186610413565b903582811691600881106105b4575b505016036105ae57816104e991817ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff6810191610413565b90357fffff00000000000000000000000000000000000000000000000000000000000081169160028110610579575b505060f01c907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc28101821161054b575090565b7f07b9a191000000000000000000000000000000000000000000000000000000005f5260045260245260445ffd5b7fffff0000000000000000000000000000000000000000000000000000000000009250829060020360031b1b16165f80610518565b50505f90565b839250829060080360031b1b16165f806104b256fea2646970667358221220759b1220b07edb09720ca400d6ed37675368c835a3dc90f6bd7b14124dafd75b64736f6c634300081c0033";
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

    public partial class InvalidPaymasterSignatureLengthError : InvalidPaymasterSignatureLengthErrorBase { }

    [Error("InvalidPaymasterSignatureLength")]
    public class InvalidPaymasterSignatureLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "dataLength", 1)]
        public virtual BigInteger DataLength { get; set; }
        [Parameter("uint256", "pmSignatureLength", 2)]
        public virtual BigInteger PmSignatureLength { get; set; }
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
