using System.Numerics;
using Nethereum.AccountAbstraction.Factory;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using GeneratedSimpleAccountCreateAccountFunction =
    Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition.CreateAccountFunction;
using GeneratedNethereumAccountCreateAccountFunction =
    Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition.CreateAccountFunction;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    public class InitCodeByteIdentityTests
    {
        [Fact]
        [Trait("UseCase", "CreateAccount")]
        public void SimpleAccountInitCodeBuilder_Matches_GeneratedSimpleAccountFactory_CreateAccountCalldata()
        {
            const string factoryAddress = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
            const string owner = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
            BigInteger salt = 424242;

            var builder = new SimpleAccountInitCodeBuilder(new FactoryConfig(factoryAddress, owner, salt));
            var actual = builder.BuildFactoryData();

            var expected = new GeneratedSimpleAccountCreateAccountFunction
            {
                Owner = owner,
                Salt = salt
            }.GetCallData();

            Assert.Equal(factoryAddress, builder.FactoryAddress);
            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        [Trait("UseCase", "CreateAccount")]
        public void NethereumAccountInitCodeBuilder_Matches_GeneratedNethereumAccountFactory_CreateAccountCalldata()
        {
            const string factoryAddress = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
            var salt = new byte[32];
            salt[31] = 7;
            var initData = Nethereum.Util.ByteUtil.Merge(
                "0x9965507D1a55bcC2695C58ba16FB37d819B0A4dc".HexToByteArray(),
                "0x70997970C51812dc3A010C7d01b50e0d17dc79C8".HexToByteArray());

            var builder = new NethereumAccountInitCodeBuilder(factoryAddress, salt, initData);
            var actual = builder.BuildFactoryData();

            var expected = new GeneratedNethereumAccountCreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            }.GetCallData();

            Assert.Equal(factoryAddress, builder.FactoryAddress);
            Assert.Equal(expected.ToHex(), actual.ToHex());
        }
    }
}
