#!/bin/bash

function build () {
  dotnet clean -f net472 -p:ReleaseSuffix=$releaseSuffix -p:TargetNet472=true -p:TargetNet35=false -p:TargetUnityNet472AOT=true
  dotnet restore -p:ReleaseSuffix=$releaseSuffix -p:TargetNet472=true -p:TargetNet35=false -p:TargetUnityNet472AOT=true
  dotnet build -c Release -f net472 -p:ReleaseSuffix=$releaseSuffix -p:TargetNet472=true -p:TargetNet35=false -p:TargetUnityNet472AOT=true
  cp -rf ./bin/Release/net472/*.dll ../compiledlibraries/net472dllsAOT
}

# packing web3 and dependencies
export releaseSuffix=
export targetNet35=false

mkdir -p compiledlibraries/net472dllsAOT/
rm -rf compiledlibraries/net472dllsAOT/*.dll

cd Nethereum.Hex
build
cd ..

cd Nethereum.ABI
build
cd ..

cd Nethereum.JsonRpc.Client
build
cd ..

cd Nethereum.RPC
build
cd ..

cd Nethereum.Web3
build
cd ..

cd Nethereum.JsonRpc.IpcClient*
build
cd ..

cd Nethereum.JsonRpc.WebSocket*
build
cd ..

cd Nethereum.JsonRpc.RpcClient*
build
cd ..

cd Nethereum.KeyStore*
build
cd ..

cd Nethereum.Quorum*
build
cd ..

cd Nethereum.Geth*
build
cd ..

cd Nethereum.Contracts*
build
cd ..

cd Nethereum.RLP*
build
cd ..

cd Nethereum.Signer
build
cd ..

cd Nethereum.Util
build
cd ..

cd Nethereum.HdWallet*
build
cd ..

cd Nethereum.Parity*
build
cd ..

cd Nethereum.Accounts*
build
cd ..

cd Nethereum.Unity
build
cd ..

cd Nethereum.Unity.Metamask
build
cd ..

cd Nethereum.RPC.Reactive
build
cd ..

cd Nethereum.Besu
build
cd ..

cd Nethereum.Signer.EIP712
build
cd..

cd Nethereum.GnosisSafe
build
cd ..

cd Nethereum.Siwe.Core
build
cd ..

cd Nethereum.BlockchainProcessing
build
cd..

cd Nethereum.Optimism
build
cd ..

cd Nethereum.UI
build
cd ..

cd Nethereum.EVM
build
cd ..

cd Nethereum.Merkle
build
cd ..

cd Nethereum.Merkle.Patricia
build
cd ..

cd Nethereum.Metamask
build
cd ..

cd Nethereum.Model
build
cd ..

cd Nethereum.Mud
build
cd ..

cd Nethereum.Mud.Contracts
build
cd ..

cd Nethereum.Util.Rest
build
cd ..

cd Nethereum.Unity.EIP6963
build
cd ..

cd Nethereum.EIP6963WalletInterop
build
cd ..

exit $ERRORLEVEL