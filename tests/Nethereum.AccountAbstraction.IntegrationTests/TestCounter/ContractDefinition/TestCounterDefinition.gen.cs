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
        public static string BYTECODE = "608080604052346015576103c2908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c806306661abd146102dd578063278ddd3c14610283578063a1b46890146101de578063a5e9585f14610196578063be65ab8c14610131578063caece693146100a55763d555654414610066575f80fd5b346100a1575f7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a1576020600254604051908152f35b5f80fd5b346100a1575f7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a15760646040517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152600c60248201527f636f756e74206661696c656400000000000000000000000000000000000000006044820152fd5b346100a15760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a15760043573ffffffffffffffffffffffffffffffffffffffff81168091036100a1575f525f602052602060405f2054604051908152f35b346100a15760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a1576004355f526001602052602060405f2054604051908152f35b346100a15760407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a15760043560243567ffffffffffffffff81116100a157366023820112156100a157806004013567ffffffffffffffff81116100a157369101602401116100a15760015b8181111561025957005b61027e9061026860025461035f565b806002555f5260016020528060405f205561035f565b61024f565b346100a1575f7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a1577ffb3b4d6258432a9a3d78dd9bffbcb6cfb1bd94f58da35fd530d08da7d1d058326020604051338152a1005b346100a1575f7ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126100a157335f525f60205260405f20546001810180911161033257335f525f60205260405f20555f80f35b7f4e487b71000000000000000000000000000000000000000000000000000000005f52601160045260245ffd5b7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff8114610332576001019056fea26469706673582212205dc8796403886cb6a9be361b354d6de44900926f53a699b1f9031e23ba2698e764736f6c634300081c0033";
        public TestCounterDeploymentBase() : base(BYTECODE) { }
        public TestCounterDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CountFunction : CountFunctionBase { }

    [Function("count")]
    public class CountFunctionBase : FunctionMessage
    {

    }

    public partial class CountFailFunction : CountFailFunctionBase { }

    [Function("countFail")]
    public class CountFailFunctionBase : FunctionMessage
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
