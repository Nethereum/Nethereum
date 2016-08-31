#Getting started with the Ethereum Client

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
There are several command line options to run geth [which can be found on their documentation](https://github.com/ethereum/go-ethereum/wiki/Command-Line-Options). 

But most important you need have enabled RPC or IPC.

You can start the HTTP JSON-RPC with the --rpc flag

```geth --rpc

change the default port (8545) and listing address (localhost) with:

```geth --rpc --rpcaddr <ip> --rpcport <portnumber>
If accessing the RPC from a browser, CORS will need to be enabled with the appropriate domain set. Otherwise, JavaScript calls are limit by the same-origin policy and requests will fail:

```geth --rpc --rpccorsdomain "http://localhost:3000"
The JSON RPC can also be started from the geth console using the admin.startRPC(addr, port) command.

### Setting up your own testnet

There is already a preconfigured tesnet in Nethereum, which [can be downloaded from github](https://github.com/Nethereum/Nethereum/tree/master/testchain)

The chain keystore in the "devChain" folder contains the keys for the preconfigured account, which is also present in the genesis file "genesis_dev.json".

* Account : 0x12890d2cce102216644c59daE5baed380d84830c
* Password: password 
* Private Key: 0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7


To start the chain there are a batch file or a shell script which can be used, both of them will reset all the data when launched.

#### Batch file
```
RD /S /Q %~dp0\devChain\chainData
RD /S /Q %~dp0\devChain\dapp
RD /S /Q %~dp0\devChain\nodes
del %~dp0\devchain\nodekey

geth  --datadir=devChain init genesis_dev.json
geth  --rpc --networkid=39318 --maxpeers=0 --datadir=devChain  --rpccorsdomain "*" --rpcapi "eth,web3,personal,net,miner,admin,debug" --ipcapi "eth,web3,personal,net,miner,admin" --verbosity 0 console  

```
[Source code](https://github.com/Nethereum/Nethereum/blob/master/testchain/startgeth.bat)

#### Shell script
```
rm -rf devChain/chainData
rm -rf devChain/dapp
rm -rf devChain/nodes
rm -rf devchain/nodekey

geth  --datadir=devChain init genesis_dev.json
geth  --rpc --networkid=39318 --maxpeers=0 --datadir=devChain  --rpccorsdomain "*" --rpcapi "eth,web3,personal,net,miner,admin" --ipcapi "eth,web3,personal,net,miner,admin" --verbosity 0 console  

```
[Source code](https://github.com/Nethereum/Nethereum/blob/master/testchain/startgeth.sh)

### Other info
If you need more information on how to setup your chain you can use this blog post
http://juan.blanco.ws/setup-your-own-tesnet-ethereum/ 
