using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.ABI.Generator.Console
{

    internal class Program
    {
        static void Main(string[] args)
        {
            var function = new OwnerOfFunction();

            function.SetValue("tokenId", new BigInteger(1));
            var functionAbi = function.GetFunctionAbi();
      
            var functionAbi2 = ABITypedRegistry.GetFunctionABI<OwnerOfFunction>();
            var valid = functionAbi.HasTheSameSignatureValues(functionAbi2);
            
            var renounce = new RenounceOwnershipFunction();
            var renounceAbi = renounce.GetFunctionAbi();
           
            var renounceAbi2 = ABITypedRegistry.GetFunctionABI<RenounceOwnershipFunction>();
            System.Console.WriteLine(renounceAbi2.Sha3Signature.ToLowerInvariant() == renounceAbi.Sha3Signature.ToLowerInvariant());

            System.Console.WriteLine(functionAbi.Sha3Signature);
            System.Console.WriteLine(functionAbi2.Sha3Signature);   
            System.Console.WriteLine(functionAbi.Sha3Signature.ToLowerInvariant() == functionAbi2.Sha3Signature.ToLowerInvariant());  
            
            var transformation = new Transformation();
            
            //functionAbi.InputParameters.Add(new Parameter("uint256", "tokenId", 1));
            var parameter = new Parameter("uint256", "tokenId", 1);

            var setTransformation = new SetTransformationFunction();
            setTransformation.NewTransformation = new Transformation();
            setTransformation.NewTransformation.SetValue("base", new BigInteger(1));
            System.Console.WriteLine(setTransformation.NewTransformation.Base);
            System.Console.WriteLine(setTransformation.NewTransformation.GetValue("base"));

            var setTransformationAbi = setTransformation.GetFunctionAbi();
            var setTransformationAbi2 = ABITypedRegistry.GetFunctionABI<SetTransformationFunction>();
            System.Console.WriteLine(setTransformationAbi.Sha3Signature.ToLowerInvariant() == setTransformationAbi2.Sha3Signature.ToLowerInvariant());

            var setTransformations = new SetTransformationsFunction();
            var setTransformationsAbi = setTransformations.GetFunctionAbi();
            var setTransformationsAbi2 = ABITypedRegistry.GetFunctionABI<SetTransformationsFunction>();
            System.Console.WriteLine(setTransformationsAbi.Sha3Signature.ToLowerInvariant() == setTransformationsAbi2.Sha3Signature.ToLowerInvariant());
        }
    }

    // The owner function message definition
    [Function("ownerOf", "address")]
    public partial class OwnerOfFunction : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public BigInteger TokenId { get; set; }
    }

    public interface IGetFunctionAbi
    {
        FunctionABI GetFunctionAbi();
        void SetValue(string parameterName, object value);
        object GetValue(string parameterName);
    }

    public interface IGetStructAbi
    {
        List<Parameter> GetParameters();
        void SetValue(string parameterName, object value);
        object GetValue(string parameterName);
    }

    public partial class TransformationsDeployment : TransformationsDeploymentBase
    {
        public TransformationsDeployment() : base(BYTECODE) { }
        public TransformationsDeployment(string byteCode) : base(byteCode) { }
    }

    public class TransformationsDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x608060405234801561001057600080fd5b5061001a3361001f565b61006f565b600080546001600160a01b038381166001600160a01b0319831681178455604051919092169283917f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09190a35050565b610a0d8061007e6000396000f3fe608060405234801561001057600080fd5b50600436106100885760003560e01c80638da5cb5b1161005b5780638da5cb5b146100e6578063c468ba9414610101578063e375410c14610114578063f2fde38b146101f157600080fd5b80634113d3ea1461008d57806359577ca0146100a2578063715018a6146100cb57806381d9e0cf146100d3575b600080fd5b6100a061009b366004610801565b610204565b005b6100b56100b036600461081a565b610536565b6040516100c2919061083c565b60405180910390f35b6100a0610655565b6100a06100e13660046108d2565b610669565b6000546040516001600160a01b0390911681526020016100c2565b6100a061010f366004610902565b610693565b61019261012236600461081a565b60016020818152600093845260408085209091529183529120805491810154600282015460038301546004840154600585015460068601546007870154600888015460099098015496979596949593949293919290919060ff80821691610100810482169162010000909104168c565b604080519c8d5260208d019b909b52998b019890985260608a0196909652608089019490945260a088019290925260c087015260e0860152610100850152151561012084015215156101408301521515610160820152610180016100c2565b6100a06101ff3660046108d2565b6106de565b61020c610757565b8060c001358160e001351180610224575060e0810135155b61029b5760405162461bcd60e51b815260206004820152603860248201527f5472616e73666f726d6174696f6e733a2074696d656f7574206d75737420626560448201527f2067726561746572207468616e20756e6c6f636b54696d65000000000000000060648201526084015b60405180910390fd5b6102ad6101a082016101808301610978565b15806102bb575060c0810135155b61032d5760405162461bcd60e51b815260206004820152603b60248201527f5472616e73666f726d6174696f6e733a20776174657220636f6c6c656374696f60448201527f6e206d757374206861766520756e6c6f636b54696d65206f66203000000000006064820152608401610292565b61033f6101a082016101808301610978565b8015610358575061035861018082016101608301610978565b156103c45760405162461bcd60e51b815260206004820152603660248201527f5472616e73666f726d6174696f6e733a20776174657220636f6c6c656374696f6044820152756e206d757374206e6f7420626520612072656369706560501b6064820152608401610292565b6040518061018001604052808260400135815260200182608001358152602001826060013581526020018260a0013581526020018260c0013581526020018260e001358152602001826101000135815260200182610120013581526020018261014001358152602001826101600160208101906104419190610978565b1515815260200161045a6101a084016101808501610978565b151581526020016104736101c084016101a08501610978565b151590528135600090815260016020818152604080842095820135845294815291849020835181559183015190820155918101516002830155606081015160038301556080810151600483015560a0810151600583015560c0810151600683015560e0810151600783015561010080820151600884015561012082015160099093018054610140840151610160909401511515620100000262ff00001994151590930261ff00199515159590951661ffff19909116179390931791909116179055565b6105a06040518061018001604052806000815260200160008152602001600081526020016000815260200160008152602001600081526020016000815260200160008152602001600081526020016000151581526020016000151581526020016000151581525090565b506000918252600160208181526040808520938552928152928290208251610180810184528154815291810154938201939093526002830154918101919091526003820154606082015260048201546080820152600582015460a0820152600682015460c0820152600782015460e082015260088201546101008083019190915260099092015460ff808216151561012084015292810483161515610140830152620100009004909116151561016082015290565b61065d610757565b61066760006107b1565b565b610671610757565b600280546001600160a01b0319166001600160a01b0392909216919091179055565b61069b610757565b60005b818110156106d9576106c78383838181106106bb576106bb61099a565b90506101c00201610204565b806106d1816109b0565b91505061069e565b505050565b6106e6610757565b6001600160a01b03811661074b5760405162461bcd60e51b815260206004820152602660248201527f4f776e61626c653a206e6577206f776e657220697320746865207a65726f206160448201526564647265737360d01b6064820152608401610292565b610754816107b1565b50565b6000546001600160a01b031633146106675760405162461bcd60e51b815260206004820181905260248201527f4f776e61626c653a2063616c6c6572206973206e6f7420746865206f776e65726044820152606401610292565b600080546001600160a01b038381166001600160a01b0319831681178455604051919092169283917f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09190a35050565b60006101c0828403121561081457600080fd5b50919050565b6000806040838503121561082d57600080fd5b50508035926020909101359150565b600061018082019050825182526020830151602083015260408301516040830152606083015160608301526080830151608083015260a083015160a083015260c083015160c083015260e083015160e0830152610100808401518184015250610120808401516108af8285018215159052565b505061014083810151151590830152610160928301511515929091019190915290565b6000602082840312156108e457600080fd5b81356001600160a01b03811681146108fb57600080fd5b9392505050565b6000806020838503121561091557600080fd5b823567ffffffffffffffff8082111561092d57600080fd5b818501915085601f83011261094157600080fd5b81358181111561095057600080fd5b8660206101c08302850101111561096657600080fd5b60209290920196919550909350505050565b60006020828403121561098a57600080fd5b813580151581146108fb57600080fd5b634e487b7160e01b600052603260045260246000fd5b6000600182016109d057634e487b7160e01b600052601160045260246000fd5b506001019056fea2646970667358221220986c8a5488e21dced45a766503f64e181afc7c05c7aa6d2e5c9d3e7300ea5ceb64736f6c634300080d0033";
        public TransformationsDeploymentBase() : base(BYTECODE) { }
        public TransformationsDeploymentBase(string byteCode) : base(byteCode) { }

    }
    public partial class Transformation : TransformationBase { }

    [Struct("Transformation")]
    public partial class TransformationBase
    {
        [Parameter("uint256", "base", 1)]
        public virtual BigInteger Base { get; set; }
        [Parameter("uint256", "input", 2)]
        public virtual BigInteger Input { get; set; }
        [Parameter("uint256", "next", 3)]
        public virtual BigInteger Next { get; set; }
        [Parameter("uint256", "inputNext", 4)]
        public virtual BigInteger InputNext { get; set; }
        [Parameter("uint256", "yield", 5)]
        public virtual BigInteger Yield { get; set; }
        [Parameter("uint256", "yieldQuantity", 6)]
        public virtual BigInteger YieldQuantity { get; set; }
        [Parameter("uint256", "unlockTime", 7)]
        public virtual BigInteger UnlockTime { get; set; }
        [Parameter("uint256", "timeout", 8)]
        public virtual BigInteger Timeout { get; set; }
        [Parameter("uint256", "timeoutYield", 9)]
        public virtual BigInteger TimeoutYield { get; set; }
        [Parameter("uint256", "timeoutYieldQuantity", 10)]
        public virtual BigInteger TimeoutYieldQuantity { get; set; }
        [Parameter("uint256", "timeoutNext", 11)]
        public virtual BigInteger TimeoutNext { get; set; }
        [Parameter("bool", "isRecipe", 12)]
        public virtual bool IsRecipe { get; set; }
        [Parameter("bool", "isWaterCollection", 13)]
        public virtual bool IsWaterCollection { get; set; }
        [Parameter("bool", "exists", 14)]
        public virtual bool Exists { get; set; }
    }

    public partial class GetTransformationFunction : GetTransformationFunctionBase { }

    [Function("getTransformation", typeof(GetTransformationOutputDTO))]
    public partial class GetTransformationFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "base", 1)]
        public virtual BigInteger Base { get; set; }
        [Parameter("uint256", "input", 2)]
        public virtual BigInteger Input { get; set; }
    }

    public partial class TransformationConfig : TransformationConfigBase { }

    public class TransformationConfigBase
    {
        [Parameter("uint256", "next", 1)]
        public virtual BigInteger Next { get; set; }
        [Parameter("uint256", "yield", 2)]
        public virtual BigInteger Yield { get; set; }
        [Parameter("uint256", "inputNext", 3)]
        public virtual BigInteger InputNext { get; set; }
        [Parameter("uint256", "yieldQuantity", 4)]
        public virtual BigInteger YieldQuantity { get; set; }
        [Parameter("uint256", "unlockTime", 5)]
        public virtual BigInteger UnlockTime { get; set; }
        [Parameter("uint256", "timeout", 6)]
        public virtual BigInteger Timeout { get; set; }
        [Parameter("uint256", "timeoutYield", 7)]
        public virtual BigInteger TimeoutYield { get; set; }
        [Parameter("uint256", "timeoutYieldQuantity", 8)]
        public virtual BigInteger TimeoutYieldQuantity { get; set; }
        [Parameter("uint256", "timeoutNext", 9)]
        public virtual BigInteger TimeoutNext { get; set; }
        [Parameter("bool", "isRecipe", 10)]
        public virtual bool IsRecipe { get; set; }
        [Parameter("bool", "isWaterCollection", 11)]
        public virtual bool IsWaterCollection { get; set; }
        [Parameter("bool", "exists", 12)]
        public virtual bool Exists { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public partial class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public partial class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SetLandFunction : SetLandFunctionBase { }

    [Function("setLand")]
    public partial class SetLandFunctionBase : FunctionMessage
    {
        [Parameter("address", "_land", 1)]
        public virtual string Land { get; set; }
    }

    public partial class SetTransformationFunction : SetTransformationFunctionBase { }

    [Function("setTransformation")]
    public partial class SetTransformationFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "newTransformation", 1)]
        public virtual Transformation NewTransformation { get; set; }
    }

    public partial class SetTransformationsFunction : SetTransformationsFunctionBase { }

    [Function("setTransformations")]
    public partial class SetTransformationsFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "newTransformations", 1)]
        public virtual List<Transformation> NewTransformations { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public partial class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class TransformationsFunction : TransformationsFunctionBase { }

    [Function("transformations", typeof(TransformationsOutputDTO))]
    public partial class TransformationsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
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

    public partial class TransformationIncompatibleError : TransformationIncompatibleErrorBase { }

    [Error("TransformationIncompatible")]
    public class TransformationIncompatibleErrorBase : IErrorDTO
    {
        [Parameter("uint256", "base", 1)]
        public virtual BigInteger Base { get; set; }
        [Parameter("uint256", "input", 2)]
        public virtual BigInteger Input { get; set; }
    }

    public partial class NotUnlockedYetError : NotUnlockedYetErrorBase { }

    [Error("notUnlockedYet")]
    public class NotUnlockedYetErrorBase : IErrorDTO
    {
        [Parameter("uint256", "timeNow", 1)]
        public virtual BigInteger TimeNow { get; set; }
        [Parameter("uint256", "unlockTime", 2)]
        public virtual BigInteger UnlockTime { get; set; }
        [Parameter("uint256", "x", 3)]
        public virtual BigInteger X { get; set; }
        [Parameter("uint256", "y", 4)]
        public virtual BigInteger Y { get; set; }
    }

    public partial class GetTransformationOutputDTO : GetTransformationOutputDTOBase { }

    [FunctionOutput]
    public class GetTransformationOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("tuple", "transformation", 1)]
        public virtual TransformationConfig Transformation { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }











    public partial class TransformationsOutputDTO : TransformationsOutputDTOBase { }

    [FunctionOutput]
    public class TransformationsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "next", 1)]
        public virtual BigInteger Next { get; set; }
        [Parameter("uint256", "yield", 2)]
        public virtual BigInteger Yield { get; set; }
        [Parameter("uint256", "inputNext", 3)]
        public virtual BigInteger InputNext { get; set; }
        [Parameter("uint256", "yieldQuantity", 4)]
        public virtual BigInteger YieldQuantity { get; set; }
        [Parameter("uint256", "unlockTime", 5)]
        public virtual BigInteger UnlockTime { get; set; }
        [Parameter("uint256", "timeout", 6)]
        public virtual BigInteger Timeout { get; set; }
        [Parameter("uint256", "timeoutYield", 7)]
        public virtual BigInteger TimeoutYield { get; set; }
        [Parameter("uint256", "timeoutYieldQuantity", 8)]
        public virtual BigInteger TimeoutYieldQuantity { get; set; }
        [Parameter("uint256", "timeoutNext", 9)]
        public virtual BigInteger TimeoutNext { get; set; }
        [Parameter("bool", "isRecipe", 10)]
        public virtual bool IsRecipe { get; set; }
        [Parameter("bool", "isWaterCollection", 11)]
        public virtual bool IsWaterCollection { get; set; }
        [Parameter("bool", "exists", 12)]
        public virtual bool Exists { get; set; }
    }


}








