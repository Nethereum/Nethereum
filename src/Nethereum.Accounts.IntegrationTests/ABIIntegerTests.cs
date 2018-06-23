using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Simple;
using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.XUnitEthereumClients;
using SolidityCallAnotherContract.Contracts.Test.CQS;
using SolidityCallAnotherContract.Contracts.TheOther.CQS;
using Xunit;
using static Nethereum.Accounts.IntegrationTests.ABIIntegerTests;

namespace Nethereum.Accounts.IntegrationTests
{
    /*
    pragma solidity ^0.4.24;
pragma experimental "ABIEncoderV2";

contract Test {

     function callManyContractsVariableReturn(address[] destination, bytes[] data) public view returns (bytes[] result){
        result = new bytes[](destination.length);
        for(uint i = 0; i < destination.length; i++){
            result[i] = callContract(destination[i], data[i]);
        }
        return result;
    }
    
    function callManyContractsSameQuery(address[] destination, bytes data) public view returns (bytes[] result){
        result = new bytes[](destination.length);
        for(uint i = 0; i < destination.length; i++) {
            result[i] = callContract(destination[i], data);
        }
        return result;
    }

    function callManyOtherContractsVariableArrayReturn(address theOther) public view returns (bytes[] result){
        result = new bytes[](3);
        result[0] = CallAnotherContract(theOther);
        result[1] = CallAnotherContract(theOther);
        result[2] = CallAnotherContract(theOther);
        return result;
    }

    function callManyOtherContractsFixedArrayReturn(address theOther) public view returns (bytes[10] result){
        result[0] = CallAnotherContract(theOther);
        result[1] = CallAnotherContract(theOther);
        result[2] = CallAnotherContract(theOther);
        return result;
    }

    function CallAnotherContract(address theOther) public view returns(bytes result) 
    {
        string memory name = "Solidity";
        string memory greeting = "Welcome something much much biggger jlkjfslkfjslkdfjsldfjasdflkjsafdlkjasdfljsadfljasdfkljasdkfljsadfljasdfldsfaj booh!";

        bytes memory callData = abi.encodeWithSignature("CallMe(string,string)", name, greeting);
        return callContract(theOther, callData);
    }

    function callContract(address contractAddress, bytes memory data) view internal  returns(bytes memory answer) {

        uint256 length = data.length;
        // uint256 size = 0;
        assembly {

            let result := call(gas(), 
                contractAddress, 
                0, 
                add(data, 0x20), 
                length, 
                //mload(data), 
                0, 
                0)

            let size := returndatasize

            answer := mload(0x40)
            
            returndatacopy(answer, 0, size)
            mstore(answer, size)
            mstore(0x40, add(answer, size))
        }

        return answer;
    }
}

contract TheOther
{
    function CallMe(string name, string greeting) public view returns(bytes test)
    {
        return abi.encodePacked("Hello ", name ," ", greeting);
    }
}
*/
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ABIFixedArrayWithDynamicAndDynamicArraysTest
    {

        //This test, decodes a bytes fixed array [10] and a bytes array.
        //Bytes is a dynamic byte[] so we have fixed with dynamic and bytes[] is a byte[][]

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ABIFixedArrayWithDynamicAndDynamicArraysTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDoFunStuff()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TheOtherDeployment>();
            var deploymentReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync();

            var deploymentCallerHandler = web3.Eth.GetContractDeploymentHandler<SolidityCallAnotherContract.Contracts.Test.CQS.TestDeployment>();
            var deploymentReceiptCaller = await deploymentCallerHandler.SendRequestAndWaitForReceiptAsync(); ;

            var callMeFunction1 = new CallMeFunction()
            {
                Name = "Hi",
                Greeting = "From the other contract"
            };
            
            var contracthandler = web3.Eth.GetContractHandler(deploymentReceiptCaller.ContractAddress);

            var callManyOthersFunctionMessage = new CallManyContractsSameQueryFunction()
            {
                Destination = new string[]{deploymentReceipt.ContractAddress, deploymentReceipt.ContractAddress , deploymentReceipt.ContractAddress }.ToList(),
                Data = callMeFunction1.GetCallData()
            };

            var returnVarByteArray = await contracthandler.QueryAsync<CallManyContractsSameQueryFunction, List<Byte[]>>(callManyOthersFunctionMessage).ConfigureAwait(false);
         

            var expected = "Hello Hi From the other contract";

            var firstVar = new StringTypeDecoder().Decode(returnVarByteArray[0]);
            var secondVar = new StringTypeDecoder().Decode(returnVarByteArray[1]);
            var thirdVar = new StringTypeDecoder().Decode(returnVarByteArray[2]);

            Assert.Equal(expected, firstVar);
            Assert.Equal(expected, secondVar);
            Assert.Equal(expected, thirdVar);

            callMeFunction1.Name = "";
            callMeFunction1.Greeting = "";

            var expectedShort = "Hello  ";
            callManyOthersFunctionMessage = new CallManyContractsSameQueryFunction()
            {
                Destination = new string[] { deploymentReceipt.ContractAddress, deploymentReceipt.ContractAddress, deploymentReceipt.ContractAddress }.ToList(),
                Data = callMeFunction1.GetCallData()
            };

            returnVarByteArray = await contracthandler.QueryAsync<CallManyContractsSameQueryFunction, List<Byte[]>>(callManyOthersFunctionMessage).ConfigureAwait(false);

            firstVar = new StringTypeDecoder().Decode(returnVarByteArray[0]);
            secondVar = new StringTypeDecoder().Decode(returnVarByteArray[1]);
            thirdVar = new StringTypeDecoder().Decode(returnVarByteArray[2]);

            Assert.Equal(expectedShort, firstVar);
            Assert.Equal(expectedShort, secondVar);
            Assert.Equal(expectedShort, thirdVar);

        }


        [Fact]
       public async void ShouldDecodeFixedWithVariableElementsAndVariableElements()
       {
           //also should be able to call another contract and get the output as bytes and bytes arrays
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TheOtherDeployment>();
            var deploymentReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync();

            var deploymentCallerHandler = web3.Eth.GetContractDeploymentHandler<SolidityCallAnotherContract.Contracts.Test.CQS.TestDeployment>();
            var deploymentReceiptCaller = await deploymentCallerHandler.SendRequestAndWaitForReceiptAsync(); ;

            var contracthandler = web3.Eth.GetContractHandler(deploymentReceiptCaller.ContractAddress);

            var callManyOthersFunctionMessage = new CallManyOtherContractsFixedArrayReturnFunction()
            {
                TheOther = deploymentReceipt.ContractAddress
            };

           var callOtherFunctionMessage = new CallAnotherContractFunction()
           {
               TheOther = deploymentReceipt.ContractAddress
           };

            var returnValue = await contracthandler.QueryRawAsync(callManyOthersFunctionMessage);
            var inHex = returnValue.ToHex();

            var expected = "Hello Solidity Welcome something much much biggger jlkjfslkfjslkdfjsldfjasdflkjsafdlkjasdfljsadfljasdfkljasdkfljsadfljasdfldsfaj booh!";

            var returnByteArray = await contracthandler.QueryAsync<CallManyOtherContractsFixedArrayReturnFunction, List<Byte[]>>(callManyOthersFunctionMessage);
            //var inHex = returnValue.ToHex();
           var first = new StringTypeDecoder().Decode(returnByteArray[0]);
           var second = new StringTypeDecoder().Decode(returnByteArray[1]);
           var third = new StringTypeDecoder().Decode(returnByteArray[2]);
           Assert.Equal(expected, first);
           Assert.Equal(expected, second);
           Assert.Equal(expected, third);

            var callManyOthersVariableFunctionMessage = new CallManyOtherContractsVariableArrayReturnFunction()
           {
               TheOther = deploymentReceipt.ContractAddress
           };

           var returnVarByteArray = await contracthandler.QueryAsync<CallManyOtherContractsVariableArrayReturnFunction, List<Byte[]>>(callManyOthersVariableFunctionMessage);
           //var inHex = returnValue.ToHex();
           var firstVar = new StringTypeDecoder().Decode(returnVarByteArray[0]);
           var secondVar = new StringTypeDecoder().Decode(returnVarByteArray[1]);
           var thirdVar = new StringTypeDecoder().Decode(returnVarByteArray[2]);

           Assert.Equal(expected, firstVar);
           Assert.Equal(expected, secondVar);
           Assert.Equal(expected, thirdVar);

            var returnValue1Call = await contracthandler.QueryAsync<CallAnotherContractFunction, byte[]>(callOtherFunctionMessage);
         
            var return1ValueString = new StringTypeDecoder().Decode(returnValue1Call);
           Assert.Equal(expected, return1ValueString);
        }
    }


    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ABIIntegerTests
    {

        //Smart contract for testing
        /*
        contract Test{
    
    function MinInt256() view public returns (int256){
        return int256((uint256(1) << 255));
    }
    
    function MaxUint() view public returns (uint256) {
        return 2**256 - 1;
    }
    
    function MaxInt256() view public returns (int256) {
        return int256(~((uint256(1) << 255)));
    }
    
    //Pass a value, what is left to be the Max, and will return -1
    function OverflowInt256ByQuantity(int256 value, int256 valueToAddToMaxInt256) view public returns (int256) {
        return (value + valueToAddToMaxInt256 + 1) + MaxInt256();
    }
    
    //Pass a value, what is left to be the Min, and will return -1
    function UnderflowInt256ByQuantity(int256 value, int256 valueToAddToMinInt256) view public returns (int256) {
        return (value + valueToAddToMinInt256 - 1) + MinInt256();
    }
    
    //This is -1
    function OverflowInt256() view public returns (int256) {
        return (MaxInt256() + 1) + MaxInt256();
    }
    
    //This is -1
    function UnderflowInt256() view public returns (int256) {
        return (MinInt256() - 1) + MinInt256();
    }
    
    function OverflowUInt256() view public returns (uint256) {
        return MaxUint() + 1;
    }
    
    //Pass a value, what is left to be the Max, and will return 0
    function OverflowUInt256ByQuantity(uint256 value, uint256 valueToAddToMaxUInt256) view public returns (uint256) {
        return value + valueToAddToMaxUInt256 + 1;
    }

}
*/


    private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ABIIntegerTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public class TestDeployment : ContractDeploymentMessage
        {

            public static string BYTECODE = "608060405234801561001057600080fd5b50610279806100206000396000f3006080604052600436106100985763ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663345bd9a3811461009d5780634b562c36146100ca57806371041b0a146100df57806372679287146100fa578063840c54d61461010f578063ab9df2e614610124578063ac6dc0801461013f578063cbf412f314610154578063d28da58014610169575b600080fd5b3480156100a957600080fd5b506100b860043560243561017e565b60408051918252519081900360200190f35b3480156100d657600080fd5b506100b8610197565b3480156100eb57600080fd5b506100b86004356024356101b2565b34801561010657600080fd5b506100b86101b9565b34801561011b57600080fd5b506100b86101cb565b34801561013057600080fd5b506100b86004356024356101ef565b34801561014b57600080fd5b506100b8610208565b34801561016057600080fd5b506100b8610223565b34801561017557600080fd5b506100b8610247565b6000610188610223565b82840160010101905092915050565b60006101a1610223565b6101a9610223565b60010101905090565b0160010190565b60006101c3610247565b600101905090565b7f800000000000000000000000000000000000000000000000000000000000000090565b60006101f96101cb565b60018385010301905092915050565b60006102126101cb565b600161021c6101cb565b0301905090565b7f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff90565b600019905600a165627a7a723058202c39408cbf8485f567816db744d5263cbf1f5fb891f795bed766ebef54807c360029";

            public TestDeployment() : base(BYTECODE) { }

            public TestDeployment(string byteCode) : base(byteCode) { }


        }

        [Function("MaxUint", "uint256")]
        public class MaxFunction : ContractMessage
        {

        }

        [Function("MaxInt256", "int256")]
        public class MaxInt256Function : ContractMessage
        {

        }

        [Function("MinInt256", "int256")]
        public class MinInt256Function : ContractMessage
        {

        }

        [Function("UnderflowInt256ByQuantity", "int256")]
        public class UnderflowInt256ByQuantityFunction : ContractMessage
        {
            [Parameter("int256", "value", 1)]
            public BigInteger Value { get; set; }
            [Parameter("int256", "valueToAddToMinInt256", 2)]
            public BigInteger ValueToAddToMinInt256 { get; set; }
        }

        [Function("OverflowInt256ByQuantity", "int256")]
        public class OverflowInt256ByQuantityFunction : ContractMessage
        {
            [Parameter("int256", "value", 1)]
            public BigInteger Value { get; set; }
            [Parameter("int256", "valueToAddToMaxInt256", 2)]
            public BigInteger ValueToAddToMaxInt256 { get; set; }
        }

        [Function("OverflowUInt256ByQuantity", "uint256")]
        public class OverflowUInt256ByQuantityFunction : ContractMessage
        {
            [Parameter("uint256", "value", 1)]
            public BigInteger Value { get; set; }
            [Parameter("uint256", "valueToAddToMaxUInt256", 2)]
            public BigInteger ValueToAddToMaxUInt256 { get; set; }
        }

        [Fact]
        public async Task MinInt256()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<MinInt256Function, BigInteger>();
            Assert.Equal(result, BigInteger.Parse("-57896044618658097711785492504343953926634992332820282019728792003956564819968"));
            Assert.Equal("RPC Response: 0x8000000000000000000000000000000000000000000000000000000000000000", 
                capturingLoggerAdapter.LastEvent.MessageObject.ToString());
        }

        [Fact]
        public async Task MaxInt256()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<MaxInt256Function, BigInteger>();
            Assert.Equal(result, BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819967"));
            Assert.Equal("RPC Response: 0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                capturingLoggerAdapter.LastEvent.MessageObject.ToString());
        }

        //This test forces an overflow to test the encoding of the values for different values on the limits.
        //If the encodign is correct the overflow will occur and the result will be -1
        /*
         function OverflowInt256ByQuantity(int256 value, int256 valueToAddToMaxInt256) view public returns (int256) {
            return (value + valueToAddToMaxInt256 + 1) + MaxInt256();
          }
         */
        [Fact]
        public async Task OverflowInt256TestingEncoding()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();

            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);

            for (var i = 1; i < 1000000; i = i + 100000)
            {
                for (var x = 0; x < 100; x++)
                {
                    var testAmount = i + x;

                    var result = await contractHandler.QueryAsync<OverflowInt256ByQuantityFunction, BigInteger>(
                        new OverflowInt256ByQuantityFunction()
                        {
                            ValueToAddToMaxInt256 = testAmount,
                            Value = IntType.MAX_INT256_VALUE - testAmount
                        }
                    ).ConfigureAwait(false);

                    Assert.Equal("RPC Response: 0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                        capturingLoggerAdapter.LastEvent.MessageObject.ToString());

                    Assert.Equal(-1, result);
                }
            }
        }


        [Fact]
        public async Task OverflowUInt256TestingEncoding()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();

            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);

            for (var i = 1; i < 1000000; i = i + 100000)
            {
                for (var x = 0; x < 100; x++)
                {
                    var testAmount = i + x;

                    var result = await contractHandler.QueryAsync<OverflowUInt256ByQuantityFunction, BigInteger>(
                        new OverflowUInt256ByQuantityFunction()
                        {
                            ValueToAddToMaxUInt256 = testAmount,
                            Value = IntType.MAX_UINT256_VALUE - testAmount
                        }
                    ).ConfigureAwait(false);

                    Assert.Equal("RPC Response: 0x0000000000000000000000000000000000000000000000000000000000000000",
                        capturingLoggerAdapter.LastEvent.MessageObject.ToString());

                    Assert.Equal(0, result);
                }
            }
        }

        //This test forces an underflow to test the encoding of the values for different values on the limits.
        //If the encodign is correct the underflow will occur and the result will be -1

        /*
        function UnderflowInt256ByQuantity(int256 value, int256 valueToAddToMinInt256) view public returns(int256)
        {
            return (value + valueToAddToMinInt256 - 1) + MinInt256();
        }
        */

        [Fact]
        public async Task UnderflowInt256TestingEncoding()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();

            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);

            for (var i = 1; i < 1000000; i = i + 100000)
            {
                for (var x = 0; x < 100; x++)
                {
                    var testAmount = i + x;
                    var result = await contractHandler.QueryAsync<UnderflowInt256ByQuantityFunction, BigInteger>(
                        new UnderflowInt256ByQuantityFunction()
                        {
                            ValueToAddToMinInt256 = testAmount * -1,
                            Value = IntType.MIN_INT256_VALUE + testAmount
                        }
                    ).ConfigureAwait(false);

                    Assert.Equal("RPC Response: 0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                        capturingLoggerAdapter.LastEvent.MessageObject.ToString());

                    Assert.Equal(-1, result);
                }
            }
        }

        [Fact]
        public async Task UMaxInt256()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<MaxFunction, BigInteger>();
            
            Assert.Equal("RPC Response: 0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                capturingLoggerAdapter.LastEvent.MessageObject.ToString());
            
            Assert.Equal(result, BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"));
        }

        public Web3.Web3 GetWeb3()
        {
            var web3 = new Web3.Web3(_ethereumClientIntegrationFixture.GetWeb3().TransactionManager.Account,
                "http://localhost:8545", LogManager.GetLogger<ILog>());
            return web3;
        }
       
    }
}