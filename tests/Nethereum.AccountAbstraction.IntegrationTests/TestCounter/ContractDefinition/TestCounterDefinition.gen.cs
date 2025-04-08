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

namespace Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition
{


    public partial class TestCounterDeployment : TestCounterDeploymentBase
    {
        public TestCounterDeployment() : base(BYTECODE) { }
        public TestCounterDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestCounterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60808060405234601557610282908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806306661abd146101f3578063278ddd3c146101b7578063a1b4689014610130578063a5e9585f14610106578063be65ab8c146100cb578063caece693146100875763d555654414610066575f80fd5b34610083575f366003190112610083576020600254604051908152f35b5f80fd5b34610083575f3660031901126100835760405162461bcd60e51b815260206004820152600c60248201526b18dbdd5b9d0819985a5b195960a21b6044820152606490fd5b34610083576020366003190112610083576004356001600160a01b03811690819003610083575f525f602052602060405f2054604051908152f35b34610083576020366003190112610083576004355f526001602052602060405f2054604051908152f35b346100835760403660031901126100835760043560243567ffffffffffffffff8111610083573660238201121561008357806004013567ffffffffffffffff811161008357369101602401116100835760015b8181111561018d57005b6101b29061019c60025461023e565b806002555f5260016020528060405f205561023e565b610183565b34610083575f366003190112610083577ffb3b4d6258432a9a3d78dd9bffbcb6cfb1bd94f58da35fd530d08da7d1d058326020604051338152a1005b34610083575f36600319011261008357335f525f60205260405f20546001810180911161022a57335f525f60205260405f20555f80f35b634e487b7160e01b5f52601160045260245ffd5b5f19811461022a576001019056fea26469706673582212202d92517034cb269f39b3d3bf02d9802e5187940b1912b9aaed27b7d83edacbfc64736f6c634300081d0033";
        public TestCounterDeploymentBase() : base(BYTECODE) { }
        public TestCounterDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CountFunction : CountFunctionBase { }

    [Function("count")]
    public class CountFunctionBase : FunctionMessage
    {

    }

    public partial class CountersFunction : CountersFunctionBase { }

    [Function("counters", "uint256")]
    public class CountersFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GasWasterFunction : GasWasterFunctionBase { }

    [Function("gasWaster")]
    public class GasWasterFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "repeat", 1)]
        public virtual BigInteger Repeat { get; set; }
        [Parameter("string", "", 2)]
        public virtual string ReturnValue2 { get; set; }
    }

    public partial class JustemitFunction : JustemitFunctionBase { }

    [Function("justemit")]
    public class JustemitFunctionBase : FunctionMessage
    {

    }

    public partial class OffsetFunction : OffsetFunctionBase { }

    [Function("offset", "uint256")]
    public class OffsetFunctionBase : FunctionMessage
    {

    }

    public partial class XxxFunction : XxxFunctionBase { }

    [Function("xxx", "uint256")]
    public class XxxFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class CalledFromEventDTO : CalledFromEventDTOBase { }

    [Event("CalledFrom")]
    public class CalledFromEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, false )]
        public virtual string Sender { get; set; }
    }



    public partial class CountersOutputDTO : CountersOutputDTOBase { }

    [FunctionOutput]
    public class CountersOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class OffsetOutputDTO : OffsetOutputDTOBase { }

    [FunctionOutput]
    public class OffsetOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class XxxOutputDTO : XxxOutputDTOBase { }

    [FunctionOutput]
    public class XxxOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
}
