using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.Optimism.Lib_AddressManager.ContractDefinition
{


    public partial class Lib_AddressManagerDeployment : Lib_AddressManagerDeploymentBase
    {
        public Lib_AddressManagerDeployment() : base(BYTECODE) { }
        public Lib_AddressManagerDeployment(string byteCode) : base(byteCode) { }
    }

    public class Lib_AddressManagerDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "608060405234801561001057600080fd5b5061001a3361001f565b61006f565b600080546001600160a01b038381166001600160a01b0319831681178455604051919092169283917f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09190a35050565b6105268061007e6000396000f3fe608060405234801561001057600080fd5b50600436106100575760003560e01c8063715018a61461005c5780638da5cb5b146100665780639b2ea4bd1461008f578063bf40fac1146100a2578063f2fde38b146100b5575b600080fd5b6100646100c8565b005b6000546001600160a01b03165b6040516001600160a01b03909116815260200160405180910390f35b61006461009d3660046103d3565b610107565b6100736100b0366004610421565b6101ca565b6100646100c336600461045e565b6101f9565b6000546001600160a01b031633146100fb5760405162461bcd60e51b81526004016100f290610480565b60405180910390fd5b6101056000610294565b565b6000546001600160a01b031633146101315760405162461bcd60e51b81526004016100f290610480565b600061013c836102e4565b6000818152600160205260409081902080546001600160a01b038681166001600160a01b03198316179092559151929350169061017a9085906104b5565b604080519182900382206001600160a01b03808716845284166020840152917f9416a153a346f93d95f94b064ae3f148b6460473c6e82b3f9fc2521b873fcd6c910160405180910390a250505050565b6000600160006101d9846102e4565b81526020810191909152604001600020546001600160a01b031692915050565b6000546001600160a01b031633146102235760405162461bcd60e51b81526004016100f290610480565b6001600160a01b0381166102885760405162461bcd60e51b815260206004820152602660248201527f4f776e61626c653a206e6577206f776e657220697320746865207a65726f206160448201526564647265737360d01b60648201526084016100f2565b61029181610294565b50565b600080546001600160a01b038381166001600160a01b0319831681178455604051919092169283917f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09190a35050565b6000816040516020016102f791906104b5565b604051602081830303815290604052805190602001209050919050565b634e487b7160e01b600052604160045260246000fd5b600082601f83011261033b57600080fd5b813567ffffffffffffffff8082111561035657610356610314565b604051601f8301601f19908116603f0116810190828211818310171561037e5761037e610314565b8160405283815286602085880101111561039757600080fd5b836020870160208301376000602085830101528094505050505092915050565b80356001600160a01b03811681146103ce57600080fd5b919050565b600080604083850312156103e657600080fd5b823567ffffffffffffffff8111156103fd57600080fd5b6104098582860161032a565b925050610418602084016103b7565b90509250929050565b60006020828403121561043357600080fd5b813567ffffffffffffffff81111561044a57600080fd5b6104568482850161032a565b949350505050565b60006020828403121561047057600080fd5b610479826103b7565b9392505050565b6020808252818101527f4f776e61626c653a2063616c6c6572206973206e6f7420746865206f776e6572604082015260600190565b6000825160005b818110156104d657602081860181015185830152016104bc565b818111156104e5576000828501525b50919091019291505056fea2646970667358221220f9cbae974e8a8bd6020faba811e8b2533148fe90fb8f8d6361bda50624fdae7764736f6c634300080b0033";
        public Lib_AddressManagerDeploymentBase() : base(BYTECODE) { }
        public Lib_AddressManagerDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class GetAddressFunction : GetAddressFunctionBase { }

    [Function("getAddress", "address")]
    public class GetAddressFunctionBase : FunctionMessage
    {
        [Parameter("string", "_name", 1)]
        public virtual string Name { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SetAddressFunction : SetAddressFunctionBase { }

    [Function("setAddress")]
    public class SetAddressFunctionBase : FunctionMessage
    {
        [Parameter("string", "_name", 1)]
        public virtual string Name { get; set; }
        [Parameter("address", "_address", 2)]
        public virtual string Address { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class AddressSetEventDTO : AddressSetEventDTOBase { }

    [Event("AddressSet")]
    public class AddressSetEventDTOBase : IEventDTO
    {
        [Parameter("string", "_name", 1, true)]
        public virtual string Name { get; set; }
        [Parameter("address", "_newAddress", 2, false)]
        public virtual string NewAddress { get; set; }
        [Parameter("address", "_oldAddress", 3, false)]
        public virtual string OldAddress { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "previousOwner", 1, true)]
        public virtual string PreviousOwner { get; set; }
        [Parameter("address", "newOwner", 2, true)]
        public virtual string NewOwner { get; set; }
    }

    public partial class GetAddressOutputDTO : GetAddressOutputDTOBase { }

    [FunctionOutput]
    public class GetAddressOutputDTOBase : IFunctionOutputDTO
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






}
