using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.StandardTokenEIP20;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.HdWallet.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TokenTransferTest
    {
        public const string Words =
            "ripple scissors kick mammal hire column oak again sun offer wealth tomorrow wagon turn fatal";

        public const string Password = "TREZOR";

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TokenTransferTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldBeAbleTransferTokensUsingTheHdWallet()
        {
            var wallet = new Wallet(Words, Password);
            var account = wallet.GetAccount(0);
            
            var web3 = new Web3.Web3(account, _ethereumClientIntegrationFixture.GetWeb3().Client);

            ulong totalSupply = 1000000;
            var contractByteCode =
                "0x6060604052341561000f57600080fd5b604051602080610711833981016040528080519150505b60018054600160a060020a03191633600160a060020a0390811691909117918290556000838155911681526002602052604090208190555b505b6106a28061006f6000396000f300606060405236156100a15763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100a6578063095ea7b31461013157806318160ddd1461016757806323b872dd1461018c578063313ce567146101c857806370a08231146101f15780638da5cb5b1461022257806395d89b4114610251578063a9059cbb146102dc578063dd62ed3e14610312575b600080fd5b34156100b157600080fd5b6100b9610349565b60405160208082528190810183818151815260200191508051906020019080838360005b838110156100f65780820151818401525b6020016100dd565b50505050905090810190601f1680156101235780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b341561013c57600080fd5b610153600160a060020a0360043516602435610380565b604051901515815260200160405180910390f35b341561017257600080fd5b61017a6103ed565b60405190815260200160405180910390f35b341561019757600080fd5b610153600160a060020a03600435811690602435166044356103f4565b604051901515815260200160405180910390f35b34156101d357600080fd5b6101db610510565b60405160ff909116815260200160405180910390f35b34156101fc57600080fd5b61017a600160a060020a0360043516610515565b60405190815260200160405180910390f35b341561022d57600080fd5b610235610534565b604051600160a060020a03909116815260200160405180910390f35b341561025c57600080fd5b6100b9610543565b60405160208082528190810183818151815260200191508051906020019080838360005b838110156100f65780820151818401525b6020016100dd565b50505050905090810190601f1680156101235780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34156102e757600080fd5b610153600160a060020a036004351660243561057a565b604051901515815260200160405180910390f35b341561031d57600080fd5b61017a600160a060020a0360043581169060243516610649565b60405190815260200160405180910390f35b60408051908101604052601a81527f4578616d706c6520466978656420537570706c7920546f6b656e000000000000602082015281565b600160a060020a03338116600081815260036020908152604080832094871680845294909152808220859055909291907f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9259085905190815260200160405180910390a35060015b92915050565b6000545b90565b600160a060020a0383166000908152600260205260408120548290108015906104445750600160a060020a0380851660009081526003602090815260408083203390941683529290522054829010155b80156104505750600082115b80156104755750600160a060020a038316600090815260026020526040902054828101115b1561050457600160a060020a0380851660008181526002602081815260408084208054899003905560038252808420338716855282528084208054899003905594881680845291905290839020805486019055917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9085905190815260200160405180910390a3506001610508565b5060005b5b9392505050565b601281565b600160a060020a0381166000908152600260205260409020545b919050565b600154600160a060020a031681565b60408051908101604052600581527f4649584544000000000000000000000000000000000000000000000000000000602082015281565b600160a060020a0333166000908152600260205260408120548290108015906105a35750600082115b80156105c85750600160a060020a038316600090815260026020526040902054828101115b1561063a57600160a060020a033381166000818152600260205260408082208054879003905592861680825290839020805486019055917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9085905190815260200160405180910390a35060016103e7565b5060006103e7565b5b92915050565b600160a060020a038083166000908152600360209081526040808320938516835292905220545b929150505600a165627a7a72305820ec01add6c7f9d88976180c397e2a5b2e9f8fc1f95f5abb00e2a4c9dbf7bcfaf20029";
            var abi =
                "[{\"constant\":true,\"inputs\":[],\"name\":\"name\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"_spender\",\"type\":\"address\"},{\"name\":\"_amount\",\"type\":\"uint256\"}],\"name\":\"approve\",\"outputs\":[{\"name\":\"success\",\"type\":\"bool\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"totalSupply\",\"outputs\":[{\"name\":\"totalSupply\",\"type\":\"uint256\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"_from\",\"type\":\"address\"},{\"name\":\"_to\",\"type\":\"address\"},{\"name\":\"_amount\",\"type\":\"uint256\"}],\"name\":\"transferFrom\",\"outputs\":[{\"name\":\"success\",\"type\":\"bool\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"decimals\",\"outputs\":[{\"name\":\"\",\"type\":\"uint8\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"_owner\",\"type\":\"address\"}],\"name\":\"balanceOf\",\"outputs\":[{\"name\":\"balance\",\"type\":\"uint256\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"owner\",\"outputs\":[{\"name\":\"\",\"type\":\"address\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"symbol\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"_to\",\"type\":\"address\"},{\"name\":\"_amount\",\"type\":\"uint256\"}],\"name\":\"transfer\",\"outputs\":[{\"name\":\"success\",\"type\":\"bool\"}],\"payable\":false,\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"_owner\",\"type\":\"address\"},{\"name\":\"_spender\",\"type\":\"address\"}],\"name\":\"allowance\",\"outputs\":[{\"name\":\"remaining\",\"type\":\"uint256\"}],\"payable\":false,\"type\":\"function\"},{\"inputs\":[{\"name\":\"totalSupply\",\"type\":\"uint256\"}],\"payable\":false,\"type\":\"constructor\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"_from\",\"type\":\"address\"},{\"indexed\":true,\"name\":\"_to\",\"type\":\"address\"},{\"indexed\":false,\"name\":\"_value\",\"type\":\"uint256\"}],\"name\":\"Transfer\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"_owner\",\"type\":\"address\"},{\"indexed\":true,\"name\":\"_spender\",\"type\":\"address\"},{\"indexed\":false,\"name\":\"_value\",\"type\":\"uint256\"}],\"name\":\"Approval\",\"type\":\"event\"}]";

            var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, contractByteCode,
                account.Address, new HexBigInteger(3000000), null, totalSupply);

            var standarErc20Service = new StandardTokenService(web3, receipt.ContractAddress);

            var pollingService = new TransactionReceiptPollingService(web3.TransactionManager);

            var transactionHash = await standarErc20Service.TransferRequestAsync(
                "0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47", 10);

            var receiptSend = await pollingService.PollForReceiptAsync(transactionHash);

            var balance =
                await standarErc20Service.BalanceOfQueryAsync("0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47");

            Assert.Equal(10, balance);
        }
    }
}