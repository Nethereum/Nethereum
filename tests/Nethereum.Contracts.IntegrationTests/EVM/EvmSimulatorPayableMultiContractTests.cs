using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.SourceInfo;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.EVM
{



    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EvmSimulatorPayableMultiContractTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EvmSimulatorPayableMultiContractTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDeployToChain_TransferEtherToContract_AndTransferSameToAnotherContract()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<PayableTestSenderDeployment>().SendRequestAndWaitForReceiptAsync();
            var payableTestSenderContractAddress = transactionReceiptDeployment.ContractAddress;
            transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<PayableReceiverContractDeployment>().SendRequestAndWaitForReceiptAsync();
            var payableReceiverContractAddress = transactionReceiptDeployment.ContractAddress;

            //current block number
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var code = await web3.Eth.GetCode.SendRequestAsync(payableTestSenderContractAddress); // runtime code;

            var payMeAndSendFunction = new PayMeAndSendFunction();
            payMeAndSendFunction.AmountToSend = 5000;
            payMeAndSendFunction.RecieverContract = payableReceiverContractAddress;
            payMeAndSendFunction.FromAddress = EthereumClientIntegrationFixture.AccountAddress;

            var callInput = payMeAndSendFunction.CreateCallInput(payableTestSenderContractAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;
            callInput.ChainId = new HexBigInteger(EthereumClientIntegrationFixture.ChainId);

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            program = await evmSimulator.ExecuteAsync(program);
            var totalBalanceReceiver = programContext.ExecutionStateService.CreateOrGetAccountExecutionState(payableReceiverContractAddress).Balance.ExecutionBalance;
            var totalBalanceSender = programContext.ExecutionStateService.CreateOrGetAccountExecutionState(payableTestSenderContractAddress).Balance.ExecutionBalance;
            Assert.Equal(5000, totalBalanceReceiver);
            Assert.Equal(0, totalBalanceSender); //Sender sends the amount sent to the receiver..

            var paidAmountFunction = new PaidAmountFunction();
            callInput = paidAmountFunction.CreateCallInput(payableTestSenderContractAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;

            programContext = new ProgramContext(callInput, executionStateService);
            var program2 = new Program(code.HexToByteArray(), programContext);
            await evmSimulator.ExecuteAsync(program2);
            var resultEncoded = program.ProgramResult.Result;
            var result = new PaidAmountOutputDTO().DecodeOutput(resultEncoded.ToHex());
            Assert.Equal(5000, result.ReturnValue1);

        }


        [Fact]
        public async void ShouldSourceMapTheTrace()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<PayableTestSenderDeployment>().SendRequestAndWaitForReceiptAsync();
            var payableTestSenderContractAddress = transactionReceiptDeployment.ContractAddress;
            transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<PayableReceiverContractDeployment>().SendRequestAndWaitForReceiptAsync();
            var payableReceiverContractAddress = transactionReceiptDeployment.ContractAddress;

            //current block number
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var code = await web3.Eth.GetCode.SendRequestAsync(payableTestSenderContractAddress); // runtime code;

            var payMeAndSendFunction = new PayMeAndSendFunction();
            payMeAndSendFunction.AmountToSend = 5000;
            payMeAndSendFunction.RecieverContract = payableReceiverContractAddress;
            payMeAndSendFunction.FromAddress = EthereumClientIntegrationFixture.AccountAddress;

            var callInput = payMeAndSendFunction.CreateCallInput(payableTestSenderContractAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;
            callInput.ChainId = new HexBigInteger(EthereumClientIntegrationFixture.ChainId);

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            program = await evmSimulator.ExecuteAsync(program);
           

            var sourceMapUtil = new SourceMapUtil();
            var sourceMaps = new Dictionary<string, List<SourceMap>>
            {
                { AddressUtil.Current.ConvertToValid20ByteAddress(payableTestSenderContractAddress).ToLower(), sourceMapUtil.UnCompressSourceMap(sourceMapPayableTestSender) },
                { AddressUtil.Current.ConvertToValid20ByteAddress(payableReceiverContractAddress).ToLower(), sourceMapUtil.UnCompressSourceMap(sourceMapPayableReceiverContract) }
            };

            var programAddressAsKey = AddressUtil.Current.ConvertToValid20ByteAddress(program.ProgramContext.AddressContract).ToLower();
            if (sourceMaps.ContainsKey(programAddressAsKey))
            {
                var sourceMap = sourceMaps[programAddressAsKey];
                for (var i = 0; i < sourceMap.Count; i++)
                {
                    program.Instructions[i].SourceMap = sourceMap[i];
                }
            }


            foreach (var programCode in program.ProgramResult.InnerContractCodeCalls)
            {
                if (sourceMaps.ContainsKey(programCode.Key))
                {
                    var sourceMap = sourceMaps[programCode.Key];
                    for (var i = 0; i < sourceMap.Count; i++)
                    {
                        programCode.Value[i].SourceMap = sourceMap[i];
                    }
                }
            }

            foreach (var trace in program.Trace)
            {
                Debug.WriteLine(trace.VMTraceStep);
                Debug.WriteLine(trace.Instruction.Instruction.ToString());
                Debug.WriteLine(trace.CodeAddress);
                if ((trace.Instruction.SourceMap.Position + trace.Instruction.SourceMap.Length) < source.Length)
                {
                    Debug.WriteLine(source.Substring(trace.Instruction.SourceMap.Position, trace.Instruction.SourceMap.Length));
                }
            }
        }

        string sourceMapPayableReceiverContract = "35:323:0:-:0;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;77:25;;;;;;;;;;;;;:::i;:::-;;;;;;;:::i;:::-;;;;;;;;250:105;;;;;;;;;;;;;:::i;:::-;;;;;;;:::i;:::-;;;;;;;;109:133;;;:::i;:::-;;;;;;;:::i;:::-;;;;;;;;77:25;;;;:::o;250:105::-;300:7;326:21;319:28;;250:105;:::o;109:133::-;152:7;185:21;172:10;:34;;;;224:10;;217:17;;109:133;:::o;7:77:1:-;44:7;73:5;62:16;;7:77;;;:::o;90:118::-;177:24;195:5;177:24;:::i;:::-;172:3;165:37;90:118;;:::o;214:222::-;307:4;345:2;334:9;330:18;322:26;;358:71;426:1;415:9;411:17;402:6;358:71;:::i;:::-;214:222;;;;:::o";
        string sourceMapPayableTestSender = "362:229:0:-:0;;;;;;;;;;;;;;;;;;;;;;;;;;396:25;;;;;;;;;;;;;:::i;:::-;;;;;;;:::i;:::-;;;;;;;;430:158;;;;;;;;;;;;;:::i;:::-;;:::i;:::-;;396:25;;;;:::o;430:158::-;535:16;:22;;;567:9;535:45;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;:::i;:::-;522:10;:58;;;;430:158;:::o;7:77:1:-;44:7;73:5;62:16;;7:77;;;:::o;90:118::-;177:24;195:5;177:24;:::i;:::-;172:3;165:37;90:118;;:::o;214:222::-;307:4;345:2;334:9;330:18;322:26;;358:71;426:1;415:9;411:17;402:6;358:71;:::i;:::-;214:222;;;;:::o;523:117::-;632:1;629;622:12;769:126;806:7;846:42;839:5;835:54;824:65;;769:126;;;:::o;901:96::-;938:7;967:24;985:5;967:24;:::i;:::-;956:35;;901:96;;;:::o;1003:126::-;1070:7;1099:24;1117:5;1099:24;:::i;:::-;1088:35;;1003:126;;;:::o;1135:182::-;1238:54;1286:5;1238:54;:::i;:::-;1231:5;1228:65;1218:93;;1307:1;1304;1297:12;1218:93;1135:182;:::o;1323:199::-;1399:5;1437:6;1424:20;1415:29;;1453:63;1510:5;1453:63;:::i;:::-;1323:199;;;;:::o;1528:389::-;1617:6;1666:2;1654:9;1645:7;1641:23;1637:32;1634:119;;;1672:79;;:::i;:::-;1634:119;1792:1;1817:83;1892:7;1883:6;1872:9;1868:22;1817:83;:::i;:::-;1807:93;;1763:147;1528:389;;;;:::o;1923:122::-;1996:24;2014:5;1996:24;:::i;:::-;1989:5;1986:35;1976:63;;2035:1;2032;2025:12;1976:63;1923:122;:::o;2051:143::-;2108:5;2139:6;2133:13;2124:22;;2155:33;2182:5;2155:33;:::i;:::-;2051:143;;;;:::o;2200:351::-;2270:6;2319:2;2307:9;2298:7;2294:23;2290:32;2287:119;;;2325:79;;:::i;:::-;2287:119;2445:1;2470:64;2526:7;2517:6;2506:9;2502:22;2470:64;:::i;:::-;2460:74;;2416:128;2200:351;;;;:::o";

        string source = 
@"pragma solidity >=0.7.0 <0.9.0;

contract PayableReceiverContract {

    uint256 public paidAmount;
    function payMe() external payable returns (uint256) {
        paidAmount = address(this).balance;
        return paidAmount;
    }

    function balanceContract() external view returns (uint256){
        return address(this).balance;
    }
}

contract PayableTestSender {
    uint256 public paidAmount;

    function payMeAndSend(PayableReceiverContract recieverContract) external payable {
        paidAmount = recieverContract.payMe { value: msg.value }();
    }
}";

        public partial class PayableTestSenderDeployment : PayableTestSenderDeploymentBase
        {
            public PayableTestSenderDeployment() : base(BYTECODE) { }
            public PayableTestSenderDeployment(string byteCode) : base(byteCode) { }
        }

        public class PayableTestSenderDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b5061026a806100206000396000f3fe6080604052600436106100295760003560e01c806312fa769f1461002e578063ebfcd76014610059575b600080fd5b34801561003a57600080fd5b50610043610075565b6040516100509190610198565b60405180910390f35b610073600480360381019061006e919061012f565b61007b565b005b60005481565b8073ffffffffffffffffffffffffffffffffffffffff1663d997ccb3346040518263ffffffff1660e01b81526004016020604051808303818588803b1580156100c357600080fd5b505af11580156100d7573d6000803e3d6000fd5b50505050506040513d601f19601f820116820180604052508101906100fc919061015c565b60008190555050565b60008135905061011481610206565b92915050565b6000815190506101298161021d565b92915050565b60006020828403121561014557610144610201565b5b600061015384828501610105565b91505092915050565b60006020828403121561017257610171610201565b5b60006101808482850161011a565b91505092915050565b610192816101f7565b82525050565b60006020820190506101ad6000830184610189565b92915050565b60006101be826101d7565b9050919050565b60006101d0826101b3565b9050919050565b600073ffffffffffffffffffffffffffffffffffffffff82169050919050565b6000819050919050565b600080fd5b61020f816101c5565b811461021a57600080fd5b50565b610226816101f7565b811461023157600080fd5b5056fea2646970667358221220df52bcf4ba99f664f715421045d5ab68e15ef874a53b2f2ccb0e3b413d36d0d864736f6c63430008070033";
            public PayableTestSenderDeploymentBase() : base(BYTECODE) { }
            public PayableTestSenderDeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class PayMeAndSendFunction : PayMeAndSendFunctionBase { }

        [Function("payMeAndSend")]
        public class PayMeAndSendFunctionBase : FunctionMessage
        {
            [Parameter("address", "recieverContract", 1)]
            public virtual string RecieverContract { get; set; }
        }

        public partial class PaidAmountFunction : PaidAmountFunctionBase { }

        [Function("paidAmount", "uint256")]
        public class PaidAmountFunctionBase : FunctionMessage
        {

        }



        public partial class PaidAmountOutputDTO : PaidAmountOutputDTOBase { }

        [FunctionOutput]
        public class PaidAmountOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public virtual BigInteger ReturnValue1 { get; set; }
        }

        public partial class PayableReceiverContractDeployment : PayableReceiverContractDeploymentBase
        {
            public PayableReceiverContractDeployment() : base(BYTECODE) { }
            public PayableReceiverContractDeployment(string byteCode) : base(byteCode) { }
        }

        public class PayableReceiverContractDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b50610120806100206000396000f3fe60806040526004361060305760003560e01c806312fa769f146035578063322a5e5f14605b578063d997ccb3146081575b600080fd5b348015604057600080fd5b506047609b565b6040516052919060c7565b60405180910390f35b348015606657600080fd5b50606d60a1565b6040516078919060c7565b60405180910390f35b608760a9565b6040516092919060c7565b60405180910390f35b60005481565b600047905090565b600047600081905550600054905090565b60c18160e0565b82525050565b600060208201905060da600083018460ba565b92915050565b600081905091905056fea2646970667358221220c7ee26e86299988c6e6079a494e2435c4c8ba6a0019cd71a910a8207cb9b393064736f6c63430008070033";
            public PayableReceiverContractDeploymentBase() : base(BYTECODE) { }
            public PayableReceiverContractDeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class PayMeFunction : PayMeFunctionBase { }

        [Function("payMe", "uint256")]
        public class PayMeFunctionBase : FunctionMessage
        {

        }

        public partial class BalanceContractFunction : BalanceContractFunctionBase { }

        [Function("balanceContract", "uint256")]
        public class BalanceContractFunctionBase : FunctionMessage
        {

        }

        public partial class BalanceContractOutputDTO : BalanceContractOutputDTOBase { }

        [FunctionOutput]
        public class BalanceContractOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public virtual BigInteger ReturnValue1 { get; set; }
        }
    }
}
