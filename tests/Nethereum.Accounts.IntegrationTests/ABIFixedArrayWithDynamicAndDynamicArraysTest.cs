using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI.Decoders;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.XUnitEthereumClients;
using SolidityCallAnotherContract.Contracts.Test.CQS;
using SolidityCallAnotherContract.Contracts.TheOther.CQS;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ABIFixedArrayWithDynamicAndDynamicArraysTest
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

   //thanks to gonzalo and alex 
    function  callContract(address contractAddress, bytes memory data)  internal view returns(bytes memory answer) {

        uint256 length = data.length;
        uint256 size = 0;
        
        assembly {
            answer := mload(0x40)

            let result := staticcall(gas(),
                contractAddress, 
                add(data, 0x20), 
                length, 
                0, 
                0)
                
            //todo return some error if result is 0

            size := returndatasize
            returndatacopy(answer, 0, size)
            mstore(answer, size)
            mstore(0x40, add(answer,size))
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

        /// This test, decodes a bytes fixed array [10] and a bytes array.
        ///Bytes is a dynamic byte[] so we have fixed with dynamic and bytes[] is a byte[][]

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ABIFixedArrayWithDynamicAndDynamicArraysTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDoSimpleMultipleQueries()
        {

        }

        [Fact]
        public async void ShouldCallDifferentContractsUsingDataBytesArraysFixedAndVariable()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();

                var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TheOtherDeployment>();
                var deploymentReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

                var deploymentCallerHandler = web3.Eth.GetContractDeploymentHandler<TestDeployment>();
                var deploymentReceiptCaller = await deploymentCallerHandler.SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

                var callMeFunction1 = new CallMeFunction()
                {
                    Name = "Hi",
                    Greeting = "From the other contract"
                };

                var contracthandler = web3.Eth.GetContractHandler(deploymentReceiptCaller.ContractAddress);

                var callManyOthersFunctionMessage = new CallManyContractsSameQueryFunction()
                {
                    Destination = new string[]
                    {
                        deploymentReceipt.ContractAddress, deploymentReceipt.ContractAddress,
                        deploymentReceipt.ContractAddress
                    }.ToList(),
                    Data = callMeFunction1.GetCallData()
                };

                var returnVarByteArray = await contracthandler
                    .QueryAsync<CallManyContractsSameQueryFunction, List<byte[]>>(callManyOthersFunctionMessage)
                    .ConfigureAwait(false);


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
                    Destination = new string[]
                    {
                        deploymentReceipt.ContractAddress, deploymentReceipt.ContractAddress,
                        deploymentReceipt.ContractAddress
                    }.ToList(),
                    Data = callMeFunction1.GetCallData()
                };

                returnVarByteArray = await contracthandler
                    .QueryAsync<CallManyContractsSameQueryFunction, List<byte[]>>(callManyOthersFunctionMessage)
                    .ConfigureAwait(false);

                firstVar = new StringTypeDecoder().Decode(returnVarByteArray[0]);
                secondVar = new StringTypeDecoder().Decode(returnVarByteArray[1]);
                thirdVar = new StringTypeDecoder().Decode(returnVarByteArray[2]);

                Assert.Equal(expectedShort, firstVar);
                Assert.Equal(expectedShort, secondVar);
                Assert.Equal(expectedShort, thirdVar);
            }

        }


        [Fact]
        public async void ShouldDecodeFixedWithVariableElementsAndVariableElements()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                //also should be able to call another contract and get the output as bytes and bytes arrays
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();

                var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TheOtherDeployment>();
                var deploymentReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

                var deploymentCallerHandler =
                    web3.Eth
                        .GetContractDeploymentHandler<SolidityCallAnotherContract.Contracts.Test.CQS.TestDeployment>();
                var deploymentReceiptCaller = await deploymentCallerHandler.SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);
                ;

                var contracthandler = web3.Eth.GetContractHandler(deploymentReceiptCaller.ContractAddress);

                var callManyOthersFunctionMessage = new CallManyOtherContractsFixedArrayReturnFunction()
                {
                    TheOther = deploymentReceipt.ContractAddress
                };

                var callOtherFunctionMessage = new CallAnotherContractFunction()
                {
                    TheOther = deploymentReceipt.ContractAddress
                };

                var returnValue = await contracthandler.QueryRawAsync(callManyOthersFunctionMessage).ConfigureAwait(false);
                var inHex = returnValue.ToHex();

                var expected =
                    "Hello Solidity Welcome something much much biggger jlkjfslkfjslkdfjsldfjasdflkjsafdlkjasdfljsadfljasdfkljasdkfljsadfljasdfldsfaj booh!";

                var returnByteArray =
                    await contracthandler.QueryAsync<CallManyOtherContractsFixedArrayReturnFunction, List<Byte[]>>(
                        callManyOthersFunctionMessage).ConfigureAwait(false);
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

                var returnVarByteArray =
                    await contracthandler.QueryAsync<CallManyOtherContractsVariableArrayReturnFunction, List<Byte[]>>(
                        callManyOthersVariableFunctionMessage).ConfigureAwait(false);
                //var inHex = returnValue.ToHex();
                var firstVar = new StringTypeDecoder().Decode(returnVarByteArray[0]);
                var secondVar = new StringTypeDecoder().Decode(returnVarByteArray[1]);
                var thirdVar = new StringTypeDecoder().Decode(returnVarByteArray[2]);

                Assert.Equal(expected, firstVar);
                Assert.Equal(expected, secondVar);
                Assert.Equal(expected, thirdVar);

                var returnValue1Call =
                    await contracthandler.QueryAsync<CallAnotherContractFunction, byte[]>(callOtherFunctionMessage).ConfigureAwait(false);

                var return1ValueString = new StringTypeDecoder().Decode(returnValue1Call);
                Assert.Equal(expected, return1ValueString);
            }
        }
    }
}