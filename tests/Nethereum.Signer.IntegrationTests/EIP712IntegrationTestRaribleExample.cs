using Nethereum.XUnitEthereumClients;
using ProtocolContracts.Contracts.OrderValidatorTest;
using ProtocolContracts.Contracts.OrderValidatorTest.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.ABI.EIP712;
using Microsoft.VisualBasic;

namespace Nethereum.Signer.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EIP712IntegrationTestRaribleExample
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EIP712IntegrationTestRaribleExample(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldHashTheSame()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var testOrder = await OrderValidatorTestService.DeployContractAndGetServiceAsync(web3, new OrderValidatorTestDeployment());
            await testOrder.OrdervalidatortestInitRequestAndWaitForReceiptAsync();
        

            var order = new Order
            {
                Maker = "0xF6fEb421f2F862fbF3ECaacA27B863242ede9837",
                MakeAsset = new Asset()
                {
                    AssetType = new AssetType()
                    {
                        AssetClass = "01020202".HexToByteArray(),
                        Data = "01".HexToByteArray(),
                    },
                    Value = 1
                },
                TakeAsset = new Asset()
                {
                    AssetType = new AssetType()
                    {
                        AssetClass = "01020202".HexToByteArray(),
                        Data = "01".HexToByteArray(),
                    },
                    Value = 1,
                },
                Taker = "0xF6fEb421f2F862fbF3ECaacA27B863242ede9837",
                Salt = 1,
                Start = 1,
                End = 1,
                Data = new byte[1] { 1 },
                DataType = new byte[4] {1,2,3,4 },

            };

            var encoder = new Eip712TypedDataEncoder();

            var assetHashExpected = await testOrder.HashAssetQueryAsync(order.TakeAsset);
            var assetHashExpectedHex = assetHashExpected.ToHex();
            var assetHashActual = encoder.HashStruct(order.TakeAsset, "Asset", typeof(Asset), typeof(AssetType));
            var assetHashActualHex = assetHashActual.ToHex();
            Assert.Equal(assetHashExpectedHex, assetHashActualHex);

            var assetEncodedType = encoder.GetEncodedType("Asset", typeof(Asset), typeof(AssetType));


            var orderHashExpected = await testOrder.HashOrderQueryAsync(order);
            var orderHashExpectedHex = orderHashExpected.ToHex();
            var orderHashActual = encoder.HashStruct(order, "Order", typeof(Order), typeof(Asset), typeof(AssetType));
            var orderHashActualHex = orderHashActual.ToHex();
            Assert.Equal(orderHashExpectedHex, orderHashActualHex);

            var orderEncodedType = encoder.GetEncodedType("Order", typeof(Order), typeof(Asset), typeof(AssetType));

            var chainId = await web3.Eth.ChainId.SendRequestAsync();
            var typedData = GetOrderTypedDefinition(testOrder.ContractHandler.ContractAddress);
            typedData.Domain.ChainId = chainId;
            typedData.SetMessage(order);

            var domainEncodedType = encoder.GetEncodedTypeDomainSeparator(typedData);


            var nameHash = await testOrder.EIP712NameHashQueryAsync();
            var nameHashHex = nameHash.ToHex();
            var versionHash = await testOrder.EIP712VersionHashQueryAsync();
            var versionHashHex = versionHash.ToHex();
            var v4domainExpected = await testOrder.DomainSeparatorV4QueryAsync();
            var v4domainExpectedHex = v4domainExpected.ToHex();
            var v4domainActual = encoder.HashDomainSeparator(typedData);
            var v4domainActualHex = v4domainActual.ToHex();
            Assert.Equal(v4domainExpectedHex, v4domainActualHex);

            var expectedResult = await testOrder.HashTypedDataV4QueryAsync(order);
            var resultActual = encoder.EncodeAndHashTypedData(order, typedData);
            var expectedResultHex = expectedResult.ToHex();
            var resultActualHex = resultActual.ToHex();
            Assert.Equal(expectedResultHex, resultActualHex);
        }

        public static TypedData<Domain> GetOrderTypedDefinition(string verifyingContract)
        {
            return new TypedData<Domain>
            {
                Domain = new Domain
                {
                    Name = "Exchange",
                    Version = "2",
                    ChainId = 1,
                    VerifyingContract = verifyingContract
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Order), typeof(Asset), typeof(AssetType)),
                PrimaryType = "Order",
            };
        }

    }

    //This integration test and example extends the OrderValidatorTest from rarible to demonstrate the
    //common scenario of validation of different hashes (and creation) using tests that extend typed data and see potential issues (might be due to order, bad types) etc
    //it also shows how to ensure that the hashes match the domain (common issue), like name / version etc
    //https://github.com/rarible/protocol-contracts/blob/master/exchange-v2/test/contracts/OrderValidatorTest.sol
    /*
     contract OrderValidatorTest is OrderValidator {
    function __OrderValidatorTest_init() external initializer {
        __OrderValidator_init_unchained();
    }

    function validateOrderTest(LibOrder.Order calldata order, bytes calldata signature) external view {
        return validate(order, signature);
    }

    function validateOrderTest2(LibOrder.Order calldata order, bytes calldata signature) external {

        return validate(order, signature);
    }

    function hashTypedDataV4(LibOrder.Order calldata order) external view virtual returns (bytes32) {
        bytes32 hash = LibOrder.hash(order);
        return _hashTypedDataV4(hash);
    }


    function domainSeparatorV4() external view returns (bytes32) {
        return _domainSeparatorV4();
    }

     function getChainId() external view returns (uint256 chainId) {
        this; // silence state mutability warning without generating bytecode - see https://github.com/ethereum/solidity/issues/2691
        // solhint-disable-next-line no-inline-assembly
        assembly {
            chainId := chainid()
        }
    } 

    function hashOrder(LibOrder.Order calldata order) external view virtual returns (bytes32) {
        return LibOrder.hash(order);
    }

    function hashAsset(LibAsset.Asset calldata asset)external view virtual returns (bytes32) {
        return LibAsset.hash(asset);
    }

    function EIP712NameHash() external virtual view returns (bytes32) {
        return _EIP712NameHash();
    }

    function EIP712VersionHash() external virtual view returns (bytes32) {
        return _EIP712VersionHash();
    }
}
     */
}