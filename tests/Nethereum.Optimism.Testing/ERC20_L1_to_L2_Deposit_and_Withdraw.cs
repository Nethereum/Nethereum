using System.Linq;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Optimism;
using Nethereum.Optimism.L1StandardBridge;
using Nethereum.Optimism.L1StandardBridge.ContractDefinition;
using Nethereum.Optimism.L2StandardBridge;
using Nethereum.Optimism.L2StandardERC20;
using Nethereum.Optimism.L2StandardERC20.ContractDefinition;
using Nethereum.Optimism.L2StandardTokenFactory.ContractDefinition;
using Nethereum.Optimism.Lib_AddressManager;

using Xunit;
using WithdrawFunction = Nethereum.Optimism.L2StandardBridge.ContractDefinition.WithdrawFunction;

namespace Nethereum.Optimism.Testing
{
    public class ERC20_L1_to_L2_Deposit_and_Withdraw
    {

        
        //This is the addres manager for the local node
        string ADDRESS_MANAGER = "0x3e4CFaa8730092552d9425575E49bB542e329981";
        string KOVAN_ADDRESS_MANAGER = "0x100Dd3b414Df5BbA2B542864fF94aF8024aFdf3a";
        //[Fact]
        public async void ShouldBeAbleToDepositErc20AndWithdrawUsingTheGateway()
        {


            var web3l1 = new Web3.Web3(new Account("YOUR PRIVATE KEY", 42), "https://kovan.infura.io/v3/3e2d593aa68042cc8cce973b4b5d23ef");
            var web3l2 = new Web3.Web3(new Account("YOUR PRIVATE KEY", 69), "https://kovan.optimism.io");
            web3l2.TransactionManager.UseLegacyAsDefault = true;

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

            var tokenName = "OPNETH";
            var tokenSymbol = "OPNETH";

            var erc20TokenDeployment = new ERC20Deployment()
                { TokenName = tokenName, InitialAmount = 100000, TokenSymbol = tokenSymbol, DecimalUnits = 18 };

            //Deploy our custom token

            var deploymentHandler = web3l1.Eth.GetContractDeploymentHandler<ERC20Deployment>();
            var tokenDeploymentReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(erc20TokenDeployment);

            var l2Erc20TokenDeployment = new L2StandardERC20Deployment();
            l2Erc20TokenDeployment.L1Token = tokenDeploymentReceipt.ContractAddress;
            l2Erc20TokenDeployment.L2Bridge = L2StandardBridgeAddress;
            l2Erc20TokenDeployment.Name = tokenName;
            l2Erc20TokenDeployment.Symbol = tokenSymbol;

            var l2Erc20TokenDeploymentReceipt = await L2StandardERC20Service.DeployContractAndWaitForReceiptAsync(web3l2, l2Erc20TokenDeployment);

            var l2StandardErc20Service = new L2StandardERC20Service(web3l2, l2Erc20TokenDeploymentReceipt.ContractAddress);
            //Creating a new service
            var tokenService = web3l1.Eth.ERC20.GetContractService(tokenDeploymentReceipt.ContractAddress);
            
            
            
            var balancesInL1 = await tokenService.BalanceOfQueryAsync(ourAdddress);
            var receiptApproval = await tokenService.ApproveRequestAndWaitForReceiptAsync(l1StandardBridgeAddress, 1);
           
            
            var receiptDeposit = await l1StandardBridgeService.DepositERC20RequestAndWaitForReceiptAsync(new DepositERC20Function()
            {
                L1Token = tokenDeploymentReceipt.ContractAddress,
                L2Token = l2Erc20TokenDeploymentReceipt.ContractAddress,
                Amount = 1, L2Gas = 2000000, Data = "0x".HexToByteArray()
            });

            balancesInL1 = await tokenService.BalanceOfQueryAsync(ourAdddress);
            //what the watcher does.. we do already have the txn receipt.. but for demo purpouses
            var messageHashes = watcher.GetMessageHashes(receiptDeposit);

            var txnReceipt = await watcher.GetCrossMessageMessageTransactionReceipt(web3l2, L2CrossDomainMessengerAddress, messageHashes.First());

            var balancesInL2 = await l2StandardErc20Service.BalanceOfQueryAsync(ourAdddress);

            Assert.Equal(1, balancesInL2);

            var withdrawErc20Token = new WithdrawFunction()
            {
                L2Token = l2Erc20TokenDeploymentReceipt.ContractAddress,
                Amount = 1,
                L1Gas = 2000000,
                Data = "0x".HexToByteArray()
            };

            var receiptWidthdraw = await l2StandardBridgeService.WithdrawRequestAndWaitForReceiptAsync(withdrawErc20Token);

             messageHashes = watcher.GetMessageHashes(receiptWidthdraw);

             balancesInL2 = await l2StandardErc20Service.BalanceOfQueryAsync(ourAdddress);

             Assert.Equal(0, balancesInL2);

            //txnReceipt = await watcher.GetCrossMessageMessageTransactionReceipt(web3l1, L1CrossDomainMessengerAddress, messageHashes.First());


        }

        public class ERC20Deployment : ContractDeploymentMessage
        {
#if !BYTECODELITE
            public static string BYTECODE =
                "608060405234801561001057600080fd5b506040516107843803806107848339810160409081528151602080840151838501516060860151336000908152808552959095208490556002849055908501805193959094919391019161006991600391860190610096565b506004805460ff191660ff8416179055805161008c906005906020840190610096565b5050505050610131565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106100d757805160ff1916838001178555610104565b82800160010185558215610104579182015b828111156101045782518255916020019190600101906100e9565b50610110929150610114565b5090565b61012e91905b80821115610110576000815560010161011a565b90565b610644806101406000396000f3006080604052600436106100ae5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100b3578063095ea7b31461013d57806318160ddd1461017557806323b872dd1461019c57806327e235e3146101c6578063313ce567146101e75780635c6581651461021257806370a082311461023957806395d89b411461025a578063a9059cbb1461026f578063dd62ed3e14610293575b600080fd5b3480156100bf57600080fd5b506100c86102ba565b6040805160208082528351818301528351919283929083019185019080838360005b838110156101025781810151838201526020016100ea565b50505050905090810190601f16801561012f5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561014957600080fd5b50610161600160a060020a0360043516602435610348565b604080519115158252519081900360200190f35b34801561018157600080fd5b5061018a6103ae565b60408051918252519081900360200190f35b3480156101a857600080fd5b50610161600160a060020a03600435811690602435166044356103b4565b3480156101d257600080fd5b5061018a600160a060020a03600435166104b7565b3480156101f357600080fd5b506101fc6104c9565b6040805160ff9092168252519081900360200190f35b34801561021e57600080fd5b5061018a600160a060020a03600435811690602435166104d2565b34801561024557600080fd5b5061018a600160a060020a03600435166104ef565b34801561026657600080fd5b506100c861050a565b34801561027b57600080fd5b50610161600160a060020a0360043516602435610565565b34801561029f57600080fd5b5061018a600160a060020a03600435811690602435166105ed565b6003805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156103405780601f1061031557610100808354040283529160200191610340565b820191906000526020600020905b81548152906001019060200180831161032357829003601f168201915b505050505081565b336000818152600160209081526040808320600160a060020a038716808552908352818420869055815186815291519394909390927f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925928290030190a350600192915050565b60025481565b600160a060020a03831660008181526001602090815260408083203384528252808320549383529082905281205490919083118015906103f45750828110155b15156103ff57600080fd5b600160a060020a038085166000908152602081905260408082208054870190559187168152208054849003905560001981101561046157600160a060020a03851660009081526001602090815260408083203384529091529020805484900390555b83600160a060020a031685600160a060020a03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef856040518082815260200191505060405180910390a3506001949350505050565b60006020819052908152604090205481565b60045460ff1681565b600160209081526000928352604080842090915290825290205481565b600160a060020a031660009081526020819052604090205490565b6005805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156103405780601f1061031557610100808354040283529160200191610340565b3360009081526020819052604081205482111561058157600080fd5b3360008181526020818152604080832080548790039055600160a060020a03871680845292819020805487019055805186815290519293927fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef929181900390910190a350600192915050565b600160a060020a039182166000908152600160209081526040808320939094168252919091522054905600a165627a7a72305820a364c08a705d8b29603263ebff0569de6c90b2d665d056a3c77729e2eda923ef0029";
#else
        public static string BYTECODE = "";
#endif
            public ERC20Deployment() : base(BYTECODE)
            {
            }

            public ERC20Deployment(string byteCode) : base(byteCode)
            {
            }

            [Parameter("uint256", "_initialAmount", 1)]
            public virtual BigInteger InitialAmount { get; set; }

            [Parameter("string", "_tokenName", 2)]
            public virtual string TokenName { get; set; }

            [Parameter("uint8", "_decimalUnits", 3)]
            public virtual byte DecimalUnits { get; set; }

            [Parameter("string", "_tokenSymbol", 4)]
            public virtual string TokenSymbol { get; set; }
        }

    }
}