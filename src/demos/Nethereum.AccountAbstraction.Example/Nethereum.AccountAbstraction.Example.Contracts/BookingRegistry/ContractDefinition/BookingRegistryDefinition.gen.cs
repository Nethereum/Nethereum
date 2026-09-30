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
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry.ContractDefinition;

namespace Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry.ContractDefinition
{


    public partial class BookingRegistryDeployment : BookingRegistryDeploymentBase
    {
        public BookingRegistryDeployment() : base(BYTECODE) { }
        public BookingRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class BookingRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608080604052346026575f80546001600160a01b0319163317905561020f908161002b8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c80631116fd041461014757806337bdc99b146100a25780634efe3f7f1461007057638da5cb5b14610045575f80fd5b3461006c575f36600319011261006c575f546040516001600160a01b039091168152602090f35b5f80fd5b3461006c57602036600319011261006c576004355f526001602052602060018060a01b0360405f205416604051908152f35b3461006c57602036600319011261006c575f54600435906001600160a01b03163303610109575f81815260016020526040812080546001600160a01b03191690557fdc1d59afba091fe396c59cfca495d22c16af8c5996c76bc3d12ca7dc540d67b79080a2005b60405162461bcd60e51b81526020600482015260166024820152756f6e6c79206f776e65722063616e2072656c6561736560501b6044820152606490fd5b3461006c57602036600319011261006c576004355f818152600160205260409020546001600160a01b0316806101c35750805f52600160205260405f20336bffffffffffffffffffffffff60a01b82541617905533907f3b8ce702eda06485c7aabdb0c387135b1dd082863c1d289161e2c08de361e38c5f80a3005b9063ebf3801160e01b5f5260045260245260445ffdfea26469706673582212204c24f85a532e39d189175080116f11fd24ba19bd9050b67af5834b1a35bf9b6a64736f6c634300081c0033";
        public BookingRegistryDeploymentBase() : base(BYTECODE) { }
        public BookingRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class BookFunction : BookFunctionBase { }

    [Function("book")]
    public class BookFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "slot", 1)]
        public virtual BigInteger Slot { get; set; }
    }

    public partial class GuestOfFunction : GuestOfFunctionBase { }

    [Function("guestOf", "address")]
    public class GuestOfFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class ReleaseFunction : ReleaseFunctionBase { }

    [Function("release")]
    public class ReleaseFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "slot", 1)]
        public virtual BigInteger Slot { get; set; }
    }



    public partial class GuestOfOutputDTO : GuestOfOutputDTOBase { }

    [FunctionOutput]
    public class GuestOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class SlotBookedEventDTO : SlotBookedEventDTOBase { }

    [Event("SlotBooked")]
    public class SlotBookedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "slot", 1, true )]
        public virtual BigInteger Slot { get; set; }
        [Parameter("address", "guest", 2, true )]
        public virtual string Guest { get; set; }
    }

    public partial class SlotReleasedEventDTO : SlotReleasedEventDTOBase { }

    [Event("SlotReleased")]
    public class SlotReleasedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "slot", 1, true )]
        public virtual BigInteger Slot { get; set; }
    }

    public partial class SlotAlreadyBookedError : SlotAlreadyBookedErrorBase { }

    [Error("SlotAlreadyBooked")]
    public class SlotAlreadyBookedErrorBase : IErrorDTO
    {
        [Parameter("uint256", "slot", 1)]
        public virtual BigInteger Slot { get; set; }
        [Parameter("address", "guest", 2)]
        public virtual string Guest { get; set; }
    }
}
