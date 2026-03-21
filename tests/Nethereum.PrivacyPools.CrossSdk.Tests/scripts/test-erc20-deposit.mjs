import { execSync } from 'child_process';

const PK = '0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80';
const RPC = 'http://127.0.0.1:18546';
const ACCOUNT = '0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266';
const CD = 'C:/Users/SuperDev/Documents/Repos/privacy-pools-core/packages/contracts';

function cast(cmd) {
  return execSync(`cast ${cmd}`, { cwd: CD, encoding: 'utf8' }).trim();
}
function send(to, sig, args = '', extra = '') {
  return cast(`send --rpc-url ${RPC} --private-key ${PK} ${extra} ${to} "${sig}" ${args}`);
}
function deploy(bytecodeFile, constructorTypes = '', constructorArgs = '', extra = '') {
  const out = cast(
    `send --rpc-url ${RPC} --private-key ${PK} --create ${extra} ` +
    `$(cat ${bytecodeFile} | node -e "const j=JSON.parse(require('fs').readFileSync('/dev/stdin','utf8'));process.stdout.write(j.bytecode.object)")` +
    (constructorTypes ? ` "${constructorTypes}" ${constructorArgs}` : '')
  );
  // Extract contract address from output
  const match = out.match(/contractAddress\s+(0x[a-fA-F0-9]+)/);
  return match ? match[1] : null;
}

// Deploy FuzzERC20 using forge create (already works)
console.log('Deploying FuzzERC20...');
const forgeOut = execSync(
  `forge create --broadcast --root "${CD}" --rpc-url ${RPC} --private-key ${PK} test/invariants/fuzz/helpers/FuzzERC20.sol:FuzzERC20`,
  { encoding: 'utf8' }
);
const tokenAddress = forgeOut.match(/Deployed to: (0x[a-fA-F0-9]+)/)[1];
console.log('Token:', tokenAddress);

// Mint
send(tokenAddress, 'mint(address,uint256)', `${ACCOUNT} 1000000000000000000000000`);
const bal = cast(`call --rpc-url ${RPC} ${tokenAddress} "balanceOf(address)(uint256)" ${ACCOUNT}`);
console.log('Balance:', bal);

// Deploy all contracts via forge create with --root
function forgeCreate(contract, constructorArgs = '') {
  const out = execSync(
    `forge create --broadcast --root "${CD}" --rpc-url ${RPC} --private-key ${PK} --gas-limit 15000000 ${contract} ${constructorArgs}`,
    { encoding: 'utf8' }
  );
  return out.match(/Deployed to: (0x[a-fA-F0-9]+)/)[1];
}

const wv = forgeCreate('src/contracts/verifiers/WithdrawalVerifier.sol:WithdrawalVerifier');
console.log('WV:', wv);
const cv = forgeCreate('src/contracts/verifiers/CommitmentVerifier.sol:CommitmentVerifier');
console.log('CV:', cv);
const ep = forgeCreate('src/contracts/Entrypoint.sol:Entrypoint');
console.log('EP impl:', ep);

// Deploy proxy via cast (OZ submodule not available for forge)
const initCalldata = cast(`calldata "initialize(address,address)" ${ACCOUNT} ${ACCOUNT}`);
// Use Nethereum's already-compiled ERC1967Proxy bytecode
const proxyBytecode = execSync(
  `node -e "const j=JSON.parse(require('fs').readFileSync('C:/Users/SuperDev/Documents/Repos/Nethereum/src/Nethereum.PrivacyPools/ERC1967Proxy/ContractDefinition/ERC1967ProxyDefinition.gen.cs','utf8').match(/BYTECODE = \\"(0x[a-f0-9]+)\\"/)[1]);process.stdout.write(j)"`,
  { encoding: 'utf8' }
).trim();
// Actually just use cast to deploy raw bytecode from Nethereum's generated code
// Simpler: encode constructor args and append to bytecode
const proxyCreationCode = execSync(
  `node -e "
    const fs = require('fs');
    const cs = fs.readFileSync('C:/Users/SuperDev/Documents/Repos/Nethereum/src/Nethereum.PrivacyPools/ERC1967Proxy/ContractDefinition/ERC1967ProxyDefinition.gen.cs','utf8');
    const m = cs.match(/BYTECODE = \\"(0x[a-f0-9]+)\\"/);
    process.stdout.write(m[1]);
  "`,
  { encoding: 'utf8' }
);

const proxyOut = execSync(
  `cast send --rpc-url ${RPC} --private-key ${PK} --gas-limit 5000000 --create ${proxyCreationCode} "constructor(address,bytes)" ${ep} ${initCalldata}`,
  { encoding: 'utf8' }
);
const proxyMatch = proxyOut.match(/contractAddress\s+(0x[a-fA-F0-9]+)/);
const proxy = proxyMatch[1];
console.log('EP proxy:', proxy);

// Deploy Poseidon libs
const pt3 = forgeCreate('node_modules/poseidon-solidity/PoseidonT3.sol:PoseidonT3');
console.log('PT3:', pt3);
const pt4 = forgeCreate('node_modules/poseidon-solidity/PoseidonT4.sol:PoseidonT4');
console.log('PT4:', pt4);

// Deploy PrivacyPoolComplex with library linking
const pool = forgeCreate(
  `src/contracts/implementations/PrivacyPoolComplex.sol:PrivacyPoolComplex ` +
  `--libraries node_modules/poseidon-solidity/PoseidonT3.sol:PoseidonT3:${pt3} ` +
  `--libraries node_modules/poseidon-solidity/PoseidonT4.sol:PoseidonT4:${pt4}`,
  `--constructor-args ${proxy} ${wv} ${cv} ${tokenAddress}`
);
console.log('Pool:', pool);

// Register pool
send(proxy, 'registerPool(address,address,uint256,uint256,uint256)', `${tokenAddress} ${pool} 0 0 0`);
console.log('Pool registered');

// Approve
send(tokenAddress, 'approve(address,uint256)', `${proxy} 1000000000000000000000000`);
const allowance = cast(`call --rpc-url ${RPC} ${tokenAddress} "allowance(address,address)(uint256)" ${ACCOUNT} ${proxy}`);
console.log('Allowance:', allowance);

// DEPOSIT ERC20
console.log('=== DEPOSITING ERC20 ===');
try {
  const depOut = send(proxy, 'deposit(address,uint256,uint256)', `${tokenAddress} 100000000000000000000 12345`, '--gas-limit 5000000');
  console.log('DEPOSIT SUCCESS!');
  // Check for status
  const statusMatch = depOut.match(/status\s+(\d+)/);
  console.log('Status:', statusMatch ? statusMatch[1] : 'unknown');
} catch (e) {
  console.log('DEPOSIT FAILED!');
  console.log(e.stderr || e.message);

  // Try to trace the call
  try {
    const calldata = cast(`calldata "deposit(address,uint256,uint256)" ${tokenAddress} 100000000000000000000 12345`);
    const traceOut = cast(`call --rpc-url ${RPC} --from ${ACCOUNT} --trace ${proxy} ${calldata} 2>&1`);
    console.log('Trace:', traceOut.substring(0, 500));
  } catch (e2) {
    console.log('Trace error:', e2.stderr || e2.message);
  }
}
