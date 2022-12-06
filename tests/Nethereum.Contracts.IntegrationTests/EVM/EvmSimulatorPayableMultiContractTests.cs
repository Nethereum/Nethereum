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
                if ((trace.Instruction.SourceMap.Position + trace.Instruction.SourceMap.Length) < source.Length && trace.Instruction.SourceMap.Position > 0)
                {
                    Debug.WriteLine(source.Substring(trace.Instruction.SourceMap.Position, trace.Instruction.SourceMap.Length));
                }
            }
        }

        string sourceMapPayableReceiverContract = "35:323:0:-:0;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;77:25;;;;;;;;;;;;;;;;;;;160::1;;;148:2;133:18;77:25:0;;;;;;;250:105;;;;;;;;;;-1:-1:-1;326:21:0;250:105;;109:133;185:21;152:7;172:34;;;109:133;";
        string sourceMapPayableTestSender = "362:229:0:-:0;;;;;;;;;;;;;;;;;;;;;;;;;;396:25;;;;;;;;;;;;;;;;;;;160::1;;;148:2;133:18;396:25:0;;;;;;;430:158;;;;;;:::i;:::-;;:::i;:::-;;;535:16;-1:-1:-1;;;;;535:22:0;;567:9;535:45;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;:::i;:::-;522:10;:58;-1:-1:-1;430:158:0:o;196:316:1:-;285:6;338:2;326:9;317:7;313:23;309:32;306:52;;;354:1;351;344:12;306:52;380:23;;-1:-1:-1;;;;;432:31:1;;422:42;;412:70;;478:1;475;468:12;412:70;501:5;196:316;-1:-1:-1;;;196:316:1:o;517:184::-;587:6;640:2;628:9;619:7;615:23;611:32;608:52;;;656:1;653;646:12;608:52;-1:-1:-1;679:16:1;;517:184;-1:-1:-1;517:184:1:o";

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
            public static string BYTECODE = "608060405234801561001057600080fd5b50610164806100206000396000f3fe6080604052600436106100295760003560e01c806312fa769f1461002e578063ebfcd76014610056575b600080fd5b34801561003a57600080fd5b5061004460005481565b60405190815260200160405180910390f35b6100696100643660046100e5565b61006b565b005b806001600160a01b031663d997ccb3346040518263ffffffff1660e01b81526004016020604051808303818588803b1580156100a657600080fd5b505af11580156100ba573d6000803e3d6000fd5b50505050506040513d601f19601f820116820180604052508101906100df9190610115565b60005550565b6000602082840312156100f757600080fd5b81356001600160a01b038116811461010e57600080fd5b9392505050565b60006020828403121561012757600080fd5b505191905056fea2646970667358221220c7e7d58c7a60f94540e1792c0cdb89172d74fcdf0f2b0f4d5646046eb1473a1464736f6c63430008090033";
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
            public static string BYTECODE = "6080604052348015600f57600080fd5b5060ac8061001e6000396000f3fe60806040526004361060305760003560e01c806312fa769f146035578063322a5e5f14605b578063d997ccb314606c575b600080fd5b348015604057600080fd5b50604960005481565b60405190815260200160405180910390f35b348015606657600080fd5b50476049565b476000819055604956fea2646970667358221220e730dbb658a9da6b18d44e355856fb5aef90abdd725d954dffb23c9120cabfe964736f6c63430008090033";
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
