using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using System;
using System.Numerics;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
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
            var payableTestSenderDeployment = new PayableTestSenderDeployment();

            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<PayableTestSenderDeployment>().SendRequestAndWaitForReceiptAsync();
            var payableTestSenderContractAddress = transactionReceiptDeployment.ContractAddress;

            var payableReceiverContractDeployment = new PayableReceiverContractDeployment();
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

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var internalStorageState = new InternalStorageState();
            var programContext = new ProgramContext(callInput, nodeDataService, internalStorageState);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            //Assert.Equal(5000, programContext.AccountsExecutionBalanceState.GetTotalBalance(payableReceiverContractAddress));
            //Assert.Equal(0, programContext.AccountsExecutionBalanceState.GetTotalBalance(payableTestSenderDeployment));

        }

        /*

        pragma solidity >=0.7.0 <0.9.0;

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

}


        */
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