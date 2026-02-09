using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using System.Numerics;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.ERC7579
{
    [CollectionDefinition(ERC7579TestFixture.ERC7579_COLLECTION)]
    public class ERC7579TestCollection : ICollectionFixture<ERC7579TestFixture>
    {
    }

    public class ERC7579TestFixture : IAsyncLifetime
    {
        public const string ERC7579_COLLECTION = "ERC7579 Test Collection";
        public const int CHAIN_ID = 31337;

        public DevChainNode Node { get; private set; } = null!;
        public IWeb3 Web3 { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public NethereumAccountFactoryService AccountFactoryService { get; private set; } = null!;
        public ECDSAValidatorService ECDSAValidatorService { get; private set; } = null!;

        public Web3Account OwnerAccount { get; private set; } = null!;
        public string OwnerAddress => OwnerAccount.Address;
        public string OwnerPrivateKey => "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public EthECKey OwnerKey { get; private set; } = null!;
        public BigInteger ChainId => CHAIN_ID;

        public async Task InitializeAsync()
        {
            OwnerAccount = new Web3Account(OwnerPrivateKey, CHAIN_ID);
            OwnerKey = new EthECKey(OwnerPrivateKey);

            var config = new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            Node = new DevChainNode(config);
            await Node.StartAsync(new[] { OwnerAddress }, Nethereum.Web3.Web3.Convert.ToWei(10000));

            Web3 = Node.CreateWeb3(OwnerAccount);

            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Web3, new EntryPointDeployment());

            ECDSAValidatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                Web3, new ECDSAValidatorDeployment());

            var factoryDeployment = new NethereumAccountFactoryDeployment
            {
                EntryPoint = EntryPointService.ContractAddress
            };

            AccountFactoryService = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                Web3, factoryDeployment);
        }

        public Task DisposeAsync()
        {
            Node?.Dispose();
            return Task.CompletedTask;
        }

        public async Task<NethereumAccountService> CreateAccountAsync(byte[] salt)
        {
            var initData = CreateInitData();
            await AccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);
            var accountAddress = await AccountFactoryService.GetAddressQueryAsync(salt, initData);
            return new NethereumAccountService(Web3, accountAddress);
        }

        public async Task<string> GetAccountAddressAsync(byte[] salt)
        {
            var initData = CreateInitData();
            return await AccountFactoryService.GetAddressQueryAsync(salt, initData);
        }

        public byte[] CreateInitData()
        {
            var validatorAddress = ECDSAValidatorService.ContractAddress.HexToByteArray();
            var ownerAddress = OwnerAddress.HexToByteArray();
            var initData = new byte[40];
            Array.Copy(validatorAddress, 0, initData, 0, 20);
            Array.Copy(ownerAddress, 0, initData, 20, 20);
            return initData;
        }

        public async Task FundAccountAsync(string address, decimal ethAmount)
        {
            await Node.SetBalanceAsync(address, Nethereum.Web3.Web3.Convert.ToWei(ethAmount));
        }

        public byte[] CreateSalt(ulong value)
        {
            var saltBytes = new byte[32];
            var valueBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(valueBytes);
            Array.Copy(valueBytes, 0, saltBytes, 24, 8);
            return saltBytes;
        }
    }
}
