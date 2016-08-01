#Getting started with Geth

Nethereum requires an Ethereum client like Geth, eth (c++), parity, etc with RPC / IPC enabled to interact with the network.

The client might be installed locally, a server you control or be a public node depending on your needs and use case. 

For example if you are just interested to retrieve existing data from contracts, or sending offline signed transations you can just use a public node. 

Here are some quick instructions to get you setup.

## Installation and configuration of the Ethereum client (Geth)

You can download the latest version stable version of geth from [Github](https://github.com/ethereum/go-ethereum/releases), installation is  as simple as extracting geth.exe from your chosen OS.

If you are using a Mac or Linux you can also use Homebrew or PPA.

### Mac
```
brew update
brew upgrade
brew tap ethereum/ethereum
brew install ethereum
```

### Linux

```
sudo apt-get install software-properties-common
sudo add-apt-repository -y ppa:ethereum/ethereum
sudo apt-get update
sudo apt-get install ethereum
```

###RPC / IPC options 
There are several command line options to run geth [which can be found on their documentantion](https://github.com/ethereum/go-ethereum/wiki/Command-Line-Options). 

But most important you need have enabled RPC or IPC.

You can start the HTTP JSON-RPC with the --rpc flag

```geth --rpc

change the default port (8545) and listing address (localhost) with:

```geth --rpc --rpcaddr <ip> --rpcport <portnumber>
If accessing the RPC from a browser, CORS will need to be enabled with the appropriate domain set. Otherwise, JavaScript calls are limit by the same-origin policy and requests will fail:

```geth --rpc --rpccorsdomain "http://localhost:3000"
The JSON RPC can also be started from the geth console using the admin.startRPC(addr, port) command.

### Setting up your own testnet






