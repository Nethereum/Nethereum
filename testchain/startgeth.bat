RD /S /Q %~dp0devChain\geth\chainData
RD /S /Q %~dp0devChain\geth\dapp
RD /S /Q %~dp0devChain\geth\nodes
del %~dp0devchain\geth\nodekey

geth.exe  --datadir=devChain init genesis_dev.json
geth.exe --mine --rpc --ws --networkid=39318 --cache=2048 --maxpeers=0 --datadir=devChain  --ipcpath "geth.ipc"  --rpccorsdomain "*" --rpcapi "eth,web3,personal,net,miner,admin,debug" --verbosity 0 console  