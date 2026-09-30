using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;


var rpcUrl = args.Length > 0 ? args[0] : "http://127.0.0.1:8545";
var privateKey = args.Length > 1 ? args[1] : "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

var chainId = (long)new Web3(rpcUrl).Eth.ChainId.SendRequestAsync().Result.Value;
var account = new Account(privateKey, chainId);
var web3 = new Web3(account, rpcUrl);

Console.WriteLine($"Deploying EntryPoint to {rpcUrl} from {account.Address} (chainId {chainId}) ...");

var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
    web3, new EntryPointDeployment());

Console.WriteLine($"ENTRYPOINT={AddressUtil.Current.ConvertToChecksumAddress(entryPointService.ContractAddress)}");
