using Nethereum.XUnitEthereumClients;
using Nethereum.Web3.Accounts;



namespace Nethereum.Mud.IntegrationTests
{
    public static class TestAccounts
    {
        public const string Account1PrivateKey = "686afc290b99aa8f2c2f6e5d568fc2e92fbb32349938fc3c43c2691d17433a87";
        public const string Account1Address = "0x142a1679A4E500Fa2C4D1feF90a37Ceb9fc28F90";

        public const string Account2PrivateKey = "e58adb8d9de87414dcc6741d31d9e84b76a54d3f416c526d8bbc1cd82b1aa2b1";
        public const string Account2Address = "0x84B17fc666f84c21beCc5194432EcFBe5EFF22f5";

        public const string Account3PrivateKey = "aa5bc10ebf5c73004e990f773608acce451fb919073a6121c2b57043682c132b";
        public const string Account3Address = "0x01C6DfCF12bEBb84b25f6090E3950e5aC438E71E";

        public const string Account4PrivateKey = "aa5bc10ebf5c73004e990f773608acce451fb919073a6121c2b57043682c1332";
        public const string Account4Address = "0x3aD4cB8649DF90A0E885789558212d06537e9b3e";

        public const string Account5PrivateKey = "aa5bc10ebf5c73004e990f773608acce451fb919073a6121c2b57043682c1334";
        public const string Account5Address = "0x58Fd77A069CC121b7787dD13848EAAb5f8f7f1A2";

        public const string Account6PrivateKey = "aa5bc10ebf5c73004e990f773608acce451fb919073a6121c2b57043682c1337";
        public const string Account6Address = "0x2b64faDae1485ba8D4590EA50A6a629176fA63B9";


        public static Web3.Web3 GetAccount1Web3()
        {
            return new Web3.Web3(new Account(Account1PrivateKey), EthereumClientIntegrationFixture.HttpUrl);
        }


        public static Web3.Web3 GetAccount2Web3()
        {
            return new Web3.Web3(new Account(Account2PrivateKey), EthereumClientIntegrationFixture.HttpUrl);
        }

        public static Web3.Web3 GetAccount3Web3()
        {
            return new Web3.Web3(new Account(Account3PrivateKey), EthereumClientIntegrationFixture.HttpUrl);
        }

        public static Web3.Web3 GetAccount4Web3()
        {
            return new Web3.Web3(new Account(Account4PrivateKey), EthereumClientIntegrationFixture.HttpUrl);
        }

        public static Web3.Web3 GetAccount5Web3()
        {
            return new Web3.Web3(new Account(Account5PrivateKey), EthereumClientIntegrationFixture.HttpUrl);
        }
    }

}
   
