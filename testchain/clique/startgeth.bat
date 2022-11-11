RD /S /Q %~dp0\devChain\geth
geth  --datadir=devChain init genesis_clique.json
geth --nodiscover  --datadir=devChain --http --http.corsdomain "*" --mine  --ws --http.api "eth,web3,personal,net,miner,admin,debug" --http.addr "0.0.0.0" --allow-insecure-unlock --unlock 0x12890d2cce102216644c59daE5baed380d84830c --password "pass.txt" --verbosity 0 console