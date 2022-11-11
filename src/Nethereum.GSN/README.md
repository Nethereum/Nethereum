# Nethereum.GSN
Gas Station Network is the ultimate solution for Ethereum decentralized applications. It removes the barrier between users and dApps but increases the complexity of dApps.

## Pre-requirements
1. [Contract should be GSN-capable](https://docs.openzeppelin.com/contracts/2.x/gsn)
2. Contract should have positive balance in RelayHub. It can be done via [CLI](https://github.com/OpenZeppelin/openzeppelin-gsn-helpers) or `RelayHubHelper` in the project
    ```
    RelayHubHelper.DepositRelayHub(
        "ED3...",                                           // Private key of sender
        "https://rinkeby.infura.io/v3/...",                 // RPC Endpoint
        "0xD216153c06E857cD7f72665E0aF1d7D82172F494",       // RelayHub address (https://gsn.openzeppelin.com/relays)
        "0x...",                                            // Contract address
        Nethereum.Web3.Web3.Convert.ToWei(1)                // Amount
    );
    ```

## Getting started
In order to add GSN support to Nethereum `Web3` instance, follow the next steps:

1. Initialize Web3 with `RpcClient` paramenter only
    ```
    var client = new RpcClient(new Uri("https://rinkeby.infura.io/v3/..."));
    web3 = new Web3(client);
    ```
2. Initialize `GSNTransactionManager`
    ```
    var options = new GSNOptions { UseGSN = true };
    var relayClient = new RelayClient(options.HttpTimeout);
    var relayHubManager = new RelayHubManager(options, web3.Eth, relayClient);
    var transactionManager = new GSNTransactionManager(
        options,
        relayHubManager,
        web3.Eth,
        web3.Client,
        relayClient,
        new DefaultRelayPolicy(),
        "ED3..."                                            // Private key of sender
    );
    ```
3. Set Interceptor of Web3 instance
    ```
    web3.Client.OverridingRequestInterceptor = new GSNTransactionInterceptor(transactionManager);
    ```

## References
0. [EIP-1613](https://github.com/ethereum/EIPs/blob/master/EIPS/eip-1613.md)
1. [Getting started with the Gas Station Network](https://docs.openzeppelin.com/openzeppelin/gsn/getting-started)
2. [Available Relays](https://gsn.openzeppelin.com/relay-hubs/0xd216153c06e857cd7f72665e0af1d7d82172f494?listRelays=true)
3. [To discuss](https://forum.openzeppelin.com/t/gsn-support-in-nethereum/1441)
