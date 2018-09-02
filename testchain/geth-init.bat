RD /S /Q %~dp0devChain\geth\chainData
RD /S /Q %~dp0devChain\geth\dapp
RD /S /Q %~dp0devChain\geth\nodes
del %~dp0devchain\geth\nodekey

geth --identity "MyTestNetNode" --nodiscover --networkid 1999 --datadir=devChain  init genesis_dev.json
