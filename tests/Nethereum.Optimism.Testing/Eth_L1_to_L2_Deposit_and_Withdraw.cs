using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Optimism;
using Nethereum.Optimism.L1StandardBridge;
using Nethereum.Optimism.L1StandardBridge.ContractDefinition;
using Nethereum.Optimism.L2StandardBridge;
using Nethereum.Optimism.L2StandardBridge.ContractDefinition;
using Nethereum.Optimism.Lib_AddressManager;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Linq;
using Xunit;

namespace Nethereum.Optimism.Testing
{

    public class Eth_L1_to_L2_Deposit_and_Withdraw
    {

        //This is the addres manager for the local node 
        string ADDRESS_MANAGER = "0x3e4CFaa8730092552d9425575E49bB542e329981";
        private string KOVAN_ADDRESS_MANAGER = "0x100Dd3b414Df5BbA2B542864fF94aF8024aFdf3a";

        //[Fact]
        public async void ShouldBeAbleToDepositEtherAndWithdrawUsingTheGateway()
        {
           
            //var web3l1 = new Web3(new Account("0x754fde3f5e60ef2c7649061e06957c29017fe21032a8017132c0078e37f6193a", 31337), "http://localhost:9545");
            //var web3l2 = new Web3(new Account("0x754fde3f5e60ef2c7649061e06957c29017fe21032a8017132c0078e37f6193a", 420), "http://localhost:8545");


            var web3l1 = new Web3.Web3(new Account("YOUR PRIVATE KEY", 42), "https://kovan.infura.io/v3/3e2d593aa68042cc8cce973b4b5d23ef");
            var web3l2 = new Web3.Web3(new Account("YOUR PRIVATE KEY", 69), "https://kovan.optimism.io");

            var ourAdddress = web3l1.TransactionManager.Account.Address;
            var watcher = new CrossMessagingWatcherService();

            var addressManagerService = new Lib_AddressManagerService(web3l1, KOVAN_ADDRESS_MANAGER);
            var L2CrossDomainMessengerAddress = await addressManagerService.GetAddressQueryAsync("L2CrossDomainMessenger");
            var L1StandardBridgeAddress = await addressManagerService.GetAddressQueryAsync(StandardAddressManagerKeys.L1StandardBridge);
            var L1CrossDomainMessengerAddress = await addressManagerService.GetAddressQueryAsync(StandardAddressManagerKeys.L1CrossDomainMessenger);
            var L2StandardBridgeAddress = PredeployedAddresses.L2StandardBridge;
            
            var l2StandardBridgeService = new L2StandardBridgeService(web3l2, L2StandardBridgeAddress);
            var l1StandardBridgeAddress = await l2StandardBridgeService.L1TokenBridgeQueryAsync();
            var l1StandardBridgeService = new L1StandardBridgeService(web3l1, l1StandardBridgeAddress);
     

            var amount = Web3.Web3.Convert.ToWei(0.05);
            var currentBalanceInL2 = await web3l2.Eth.GetBalance.SendRequestAsync(ourAdddress);
            var depositEther = new DepositETHFunction()
            {
                AmountToSend = amount,
                L2Gas = 700000,
                Data = "0x".HexToByteArray()
            };

            var estimateGas = await l1StandardBridgeService.ContractHandler.EstimateGasAsync(depositEther);

            var receiptDeposit = await l1StandardBridgeService.DepositETHRequestAndWaitForReceiptAsync(depositEther);

            var messageHashes = watcher.GetMessageHashes(receiptDeposit);

            var txnReceipt = await watcher.GetCrossMessageMessageTransactionReceipt(web3l2, L2CrossDomainMessengerAddress, messageHashes.First());


            if (txnReceipt.HasErrors() == true)
            {
                var error =
                     await web3l2.Eth.GetContractTransactionErrorReason.SendRequestAsync(txnReceipt.TransactionHash);
                //throw new Exception(error);
            }

            var balancesInL2 = await web3l2.Eth.GetBalance.SendRequestAsync(ourAdddress); ;

            Assert.Equal(amount, balancesInL2.Value - currentBalanceInL2.Value);

            var withdrawEther = new WithdrawFunction()
            {
                L2Token = TokenAddresses.ETH,
                Amount = amount,
                //AmountToSend = amount,
                L1Gas = 700000,
                Data = "0x".HexToByteArray()
            };
            var receiptWidthdraw = await l2StandardBridgeService.WithdrawRequestAndWaitForReceiptAsync(withdrawEther);

            messageHashes = watcher.GetMessageHashes(receiptWidthdraw);

            //txnReceipt = await watcher.GetCrossMessageMessageTransactionReceipt(web3l1, L1CrossDomainMessengerAddress, messageHashes.First());

            //balancesInL2 = await web3l2.Eth.GetBalance.SendRequestAsync(ourAdddress);

            //Assert.Equal(currentBalanceInL2, balancesInL2);
        }

    }
}
