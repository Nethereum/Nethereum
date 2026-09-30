using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var rpcUrl = args.Length > 0 ? args[0] : "http://127.0.0.1:8545";
var privateKey = args.Length > 1 ? args[1] : "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

// Query the node's chainId so deployment tx signing matches whatever chain the harness runs
// (the eip7702 spec-tests require chainId 1337, other suites are chainId-agnostic).
var chainId = (long)new Web3(rpcUrl).Eth.ChainId.SendRequestAsync().Result.Value;
var account = new Account(privateKey, chainId);
var web3 = new Web3(account, rpcUrl);

Console.WriteLine($"Deploying to {rpcUrl} from {account.Address} ...");

var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
    web3, new EntryPointDeployment());

// Checksummed (EIP-55): the spec-tests' web3.py rejects non-checksummed addresses as invalid ENS names.
Console.WriteLine($"ENTRYPOINT={AddressUtil.Current.ConvertToChecksumAddress(entryPointService.ContractAddress)}");

var factoryDeployment = new SimpleAccountFactoryDeployment
{
    EntryPoint = entryPointService.ContractAddress
};

var factoryService = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
    web3, factoryDeployment);

Console.WriteLine($"FACTORY={AddressUtil.Current.ConvertToChecksumAddress(factoryService.ContractAddress)}");
