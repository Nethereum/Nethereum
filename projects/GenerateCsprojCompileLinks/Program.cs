using System;
using System.Diagnostics;

namespace GenerateCsprojCompileLinks
{
    class Program
    {
        static void Main(string[] args)
        {
            GenerateBrowserLiteFx();
            //GenerateLiteFxRemove();
            //GenerateLiteFxExcludeTests();
        }

      

        public static void GenerateBrowserLiteFx()
        {
            var stringBuilder = new System.Text.StringBuilder();
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.Hex", "Hex"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.ABI", "ABI"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.RPC", "RPC"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.Web3", "Web3"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.Model", "Model"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.StandardTokenEIP20", "EIP20"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.ENS", "ENS"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.StandardNonFungibleTokenERC721", "ERC721"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.JsonRpc.Client", "NethereumJsonRpc"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.JsonRpc.RpcClient", "NethereumJsonRpcClient"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.Contracts", "Contracts"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.Util", "Util"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.RLP", "RLP"));
            stringBuilder.Append(AddProject("..\\..\\src\\Nethereum.BlockchainProcessing", "BlockchainProcessing"));

            Debug.WriteLine(stringBuilder.ToString());
        }

        public static void GenerateLiteFxRemove()
        {
            var stringBuilder = new System.Text.StringBuilder();
            stringBuilder.Append(AddRemove("Nethereum.Hex", "Hex"));
            stringBuilder.Append(AddRemove("Nethereum.ABI", "ABI"));
            stringBuilder.Append(AddRemove("Nethereum.RPC", "RPC"));
            stringBuilder.Append(AddRemove("Nethereum.Web3", "Web3"));
            stringBuilder.Append(AddRemove("Nethereum.Model", "Model"));
            stringBuilder.Append(AddRemove("Nethereum.StandardTokenEIP20", "EIP20"));
            stringBuilder.Append(AddRemove("Nethereum.ENS", "ENS"));
            stringBuilder.Append(AddRemove("Nethereum.StandardNonFungibleTokenERC721", "ERC721"));
            stringBuilder.Append(AddRemove("Nethereum.JsonRpc.Client", "NethereumJsonRpc"));
            stringBuilder.Append(AddRemove("Nethereum.JsonRpc.RpcClient", "NethereumJsonRpcClient"));
            stringBuilder.Append(AddRemove("Nethereum.JsonRpc.IpcClient", "NethereumJsonIpcClient"));
            stringBuilder.Append(AddRemove("Nethereum.JsonRpc.WebSocketClient", "NethereumJsonWebSocketClient"));
            stringBuilder.Append(AddRemove("Nethereum.RPC.Reactive", "NethereumRPCReactive"));
            stringBuilder.Append(AddRemove("Nethereum.KeyStore", "KeyStore"));
            stringBuilder.Append(AddRemove("Nethereum.Quorum", "Quorum"));
            stringBuilder.Append(AddRemove("Nethereum.Geth", "Geth"));
            stringBuilder.Append(AddRemove("Nethereum.Contracts", "Contracts"));
            stringBuilder.Append(AddRemove("Nethereum.Util", "Util"));
            stringBuilder.Append(AddRemove("Nethereum.Signer", "Signer"));
            stringBuilder.Append(AddRemove("Nethereum.Signer.EIP712", "SignerEIP712"));
            stringBuilder.Append(AddRemove("Nethereum.RLP", "RLP"));
            stringBuilder.Append(AddRemove("Nethereum.Parity", "Parity"));
            stringBuilder.Append(AddRemove("Nethereum.Parity.Reactive", "ParityReactive"));
            stringBuilder.Append(AddRemove("Nethereum.Accounts", "Accounts"));
            stringBuilder.Append(AddRemove("Nethereum.BlockchainProcessing", "BlockchainProcessing"));
            stringBuilder.Append(AddRemove("Nethereum.Besu", "Besu"));
            stringBuilder.Append(AddRemove("Nethereum.RSK", "RSK"));
            stringBuilder.Append(AddRemove("Nethereum.HdWallet", "HdWallet"));


            Debug.WriteLine(stringBuilder.ToString());
        }





        public static void GenerateLiteFxExcludeTests()
        {
            var stringBuilder = new System.Text.StringBuilder();

            stringBuilder.Append(AddRemove("Nethereum.ABI.UnitTests", "ABIUnit"));
            stringBuilder.Append(AddRemove("Nethereum.Accounts.IntegrationTests", "AccountInt"));
            stringBuilder.Append(AddRemove("Nethereum.Contracts.IntegrationTests", "ContractsInt"));
            stringBuilder.Append(AddRemove("Nethereum.Contracts.UnitTests", "ContractsUnit"));
            stringBuilder.Append(AddRemove("Nethereum.HdWallet.IntegrationTests", "HdWalletInt"));
            stringBuilder.Append(AddRemove("Nethereum.HdWallet.UnitTests", "HdWalletUnit"));
            stringBuilder.Append(AddRemove("Nethereum.KeyStore.UnitTests", "KeyStoreUnit"));
            stringBuilder.Append(AddRemove("Nethereum.BlockchainProcessing.UnitTests", "BlockchainProcessingUnit"));
            stringBuilder.Append(AddRemove("Nethereum.RLP.UnitTests", "RLPUnit"));
            stringBuilder.Append(AddRemove("Nethereum.RPC.UnitTests", "RPCUnit"));
            stringBuilder.Append(AddRemove("Nethereum.Rsk.IntegrationTests", "RSKInt"));
            stringBuilder.Append(AddRemove("Nethereum.Signer.IntegrationTests", "SignerInt"));
            stringBuilder.Append(AddRemove("Nethereum.Signer.UnitTests", "SignerUnit"));
            stringBuilder.Append(AddRemove("Nethereum.ENS.IntegrationTests", "ENSInt"));
            stringBuilder.Append(AddRemove("Nethereum.StandardTokenEIP20.IntegrationTests", "ERC20Int"));
            stringBuilder.Append(AddRemove("Nethereum.XUnitEthereumClients", "XUnitEthereumClients"));
            stringBuilder.Append(AddRemove("Nethereum.RPC.Reactive.UnitTests", "ReactiveUnit"));
            stringBuilder.Append(AddRemove("Nethereum.Util.UnitTests", "UtilUnit"));

            Debug.WriteLine(stringBuilder.ToString());
        }

 

        public static string AddProject(string path, string name)
        {
            return @$"
                <Compile Include=""{path}\**\*.cs"" Exclude=""{path}\Properties\**;{path}\bin\**;{path}\obj\**"">
                   <Link>{name}\%(RecursiveDir)%(FileName)%(Extension)</Link>
                </Compile>
                 ";
        }

        public static string AddRemove(string path, string name)
        {
            return @$"
                <Compile Remove=""{path}\Properties\**;{path}\bin\**;{path}\obj\**""/>
                <None Remove=""{path}\Properties\**;{path}\bin\**;{path}\obj\**""/>
                 ";
        }

        //public static void GenerateLiteFx()
        //{
        //    var stringBuilder = new System.Text.StringBuilder();
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Hex", "Hex"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.ABI", "ABI"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.RPC", "RPC"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Web3", "Web3"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Model", "Model"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.StandardTokenEIP20", "EIP20"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.ENS", "ENS"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.StandardNonFungibleTokenERC721", "ERC721"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.JsonRpc.Client", "NethereumJsonRpc"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.JsonRpc.RpcClient", "NethereumJsonRpcClient"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.JsonRpc.IpcClient", "NethereumJsonIpcClient"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.JsonRpc.WebSocketClient", "NethereumJsonWebSocketClient"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.RPC.Reactive", "NethereumRPCReactive"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.KeyStore", "KeyStore"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Quorum", "Quorum"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Geth", "Geth"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Contracts", "Contracts"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Util", "Util"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Signer", "Signer"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Signer.EIP712", "SignerEIP712"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.RLP", "RLP"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Parity", "Parity"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Parity.Reactive", "ParityReactive"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Accounts", "Accounts"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.BlockchainProcessing", "BlockchainProcessing"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.Besu", "Besu"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.RSK", "RSK"));
        //    stringBuilder.Append(AddProject("..\\src\\Nethereum.HdWallet", "HdWallet"));


        //    Debug.WriteLine(stringBuilder.ToString());
        //}


        //public static void GenerateLiteFxTests()
        //{
        //    var stringBuilder = new System.Text.StringBuilder();

        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.ABI.UnitTests", "ABIUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Accounts.IntegrationTests", "AccountInt"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Contracts.IntegrationTests", "ContractsInt"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Contracts.UnitTests", "ContractsUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.HdWallet.IntegrationTests", "HdWalletInt"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.HdWallet.UnitTests", "HdWalletUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.KeyStore.UnitTests", "KeyStoreUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.BlockchainProcessing.UnitTests", "BlockchainProcessingUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.RLP.UnitTests", "RLPUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.RPC.UnitTests", "RPCUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Rsk.IntegrationTests", "RSKInt"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Signer.IntegrationTests", "SignerInt"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Signer.UnitTests", "SignerUnit"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.ENS.IntegrationTests", "ENSInt"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.StandardTokenEIP20.IntegrationTests", "ERC20Int"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.XUnitEthereumClients", "XUnitEthereumClients"));
        //    stringBuilder.Append(AddUnitProject("..\\tests\\Nethereum.Util.UnitTests", "UtilUnit"));

        //    Debug.WriteLine(stringBuilder.ToString());
        //}
    }
}
