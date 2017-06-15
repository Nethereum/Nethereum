RD /S /Q %~dp0\devChain\geth\chainData
RD /S /Q %~dp0\devChain\geth\dapp
RD /S /Q %~dp0\devChain\geth\nodes
del %~dp0\devchain\geth\nodekey

geth  --datadir=devChain init genesis_clique.json
geth  --rpc --datadir=devChain  --rpccorsdomain "*" --rpcapi "eth,web3,personal,net,miner,admin,debug" --verbosity 0 console  