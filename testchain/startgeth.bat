RD /S /Q %~dp0\devChain\geth\chainData
RD /S /Q %~dp0\devChain\geth\dapp
RD /S /Q %~dp0\devChain\geth\nodes
del %~dp0\devchain\geth\nodekey

geth.exe  --datadir=devChain init genesis_dev.json
geth.exe --mine --rpc --networkid=39318 --cache=2048 --maxpeers=0 --datadir=devChain  --rpccorsdomain "*" --rpcapi "eth,web3,personal,net,miner,admin,debug" --verbosity 0 console  