import {
  generateMasterKeys,
  generateDepositSecrets,
  generateWithdrawalSecrets,
  hashPrecommitment,
  getCommitment,
  generateMerkleProof,
  calculateContext
} from '@0xbow/privacy-pools-core-sdk';
import { bytesToBigInt, parseEventLogs } from 'viem';
import { mnemonicToAccount } from 'viem/accounts';
import { poseidon } from 'maci-crypto/build/ts/hashing.js';
import { fullProve } from './snarkjs-cli.mjs';
import { createClients, entrypointAbi, poolAbi, formatProofForSolidity, buildRelayData } from './helpers.mjs';
import { writeFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';

import { readInput } from './helpers.mjs';

const input = readInput();
const mnemonic = 'test test test test test test test test test test test junk';
const privateKey = '0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80';
const rpcUrl = 'http://127.0.0.1:18546';
const chainId = 1337;

function bytesToNumberLossy(bytes) {
  let value = 0;
  for (let i = 0; i < bytes.length; i++) value = value * 256 + bytes[i];
  return value;
}

const { entrypointAddress, poolAddress, artifactsDir } = input;
const scope = BigInt(input.scope);

const { publicClient, walletClient } = createClients(rpcUrl, chainId, privateKey);

console.log('=== Step 1: Legacy deposit ===');

const legacyKey1 = bytesToNumberLossy(mnemonicToAccount(mnemonic, { accountIndex: 0 }).getHdKey().privateKey);
const legacyKey2 = bytesToNumberLossy(mnemonicToAccount(mnemonic, { accountIndex: 1 }).getHdKey().privateKey);
const legacyMN = poseidon([BigInt(legacyKey1)]);
const legacyMS = poseidon([BigInt(legacyKey2)]);
const legacyKeys = { masterNullifier: legacyMN, masterSecret: legacyMS };

console.log('Legacy MN:', legacyMN.toString());
console.log('Legacy MS:', legacyMS.toString());

const depositValue = 1000000000000000000n; // 1 ETH
const depositIndex = 50n;
const { nullifier: depNullifier, secret: depSecret } = generateDepositSecrets(legacyKeys, scope, depositIndex);
const precommitment = hashPrecommitment(depNullifier, depSecret);

console.log('Deposit nullifier:', depNullifier.toString());
console.log('Deposit secret:', depSecret.toString());
console.log('Precommitment:', precommitment.toString());

const depositTx = await walletClient.writeContract({
  address: entrypointAddress,
  abi: entrypointAbi,
  functionName: 'deposit',
  args: [precommitment],
  value: depositValue,
  gas: 5_000_000n
});

const depositReceipt = await publicClient.waitForTransactionReceipt({ hash: depositTx });
const depositLogs = parseEventLogs({ abi: poolAbi, logs: depositReceipt.logs, eventName: 'Deposited' });
const label = depositLogs[0].args.label;
const onChainCommitment = depositLogs[0].args.commitment;

console.log('Deposit success! label:', label.toString());
console.log('On-chain commitment:', onChainCommitment.toString());

const commitment = getCommitment(depositValue, label, depNullifier, depSecret);
console.log('Computed commitment:', commitment.hash.toString());
console.log('Match:', commitment.hash === onChainCommitment);

console.log('\n=== Step 2: Update ASP root ===');
const aspLeaves = [label];
const aspProof = generateMerkleProof(aspLeaves, label);
const aspRoot = aspProof.root;

try {
  const updateTx = await walletClient.writeContract({
    address: entrypointAddress,
    abi: entrypointAbi,
    functionName: 'updateRoot',
    args: [aspRoot, 'bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu'],
    gas: 500_000n
  });
  const updateReceipt = await publicClient.waitForTransactionReceipt({ hash: updateTx });
  console.error('ASP root update status:', updateReceipt.status, 'gasUsed:', updateReceipt.gasUsed?.toString());
} catch (e) {
  console.error('ASP root update FAILED:', e.shortMessage || e.message);
  console.log(JSON.stringify({ success: false, error: 'ASP root update failed: ' + (e.shortMessage || e.message) }));
  process.exit(0);
}
console.error('ASP root updated:', aspRoot.toString());

console.log('\n=== Step 3: Withdraw with SAFE keys ===');
const safeKeys = generateMasterKeys(mnemonic);
console.log('Safe MN:', safeKeys.masterNullifier.toString());
console.log('Safe MS:', safeKeys.masterSecret.toString());

const withdrawnValue = depositValue / 2n;
const { nullifier: newNullifier, secret: newSecret } = generateWithdrawalSecrets(safeKeys, label, 0n);

const stateLeaves = [onChainCommitment];
const stateProof = generateMerkleProof(stateLeaves, onChainCommitment);

const relayData = buildRelayData(walletClient.account.address, walletClient.account.address, '0');
const withdrawal = { processooor: entrypointAddress, data: relayData };
const context = calculateContext(withdrawal, scope);

const witnessInput = {
  existingValue: depositValue.toString(),
  existingNullifier: depNullifier.toString(),
  existingSecret: depSecret.toString(),
  label: label.toString(),
  newNullifier: newNullifier.toString(),
  newSecret: newSecret.toString(),
  withdrawnValue: withdrawnValue.toString(),
  stateRoot: stateProof.root.toString(),
  stateTreeDepth: BigInt(stateProof.siblings.filter(s => s !== 0n).length || 1).toString(),
  stateSiblings: stateProof.siblings.map(s => s.toString()),
  stateIndex: '0',
  ASPRoot: aspProof.root.toString(),
  ASPTreeDepth: BigInt(aspProof.siblings.filter(s => s !== 0n).length || 1).toString(),
  ASPSiblings: aspProof.siblings.map(s => s.toString()),
  ASPIndex: '0',
  context: BigInt(context).toString()
};

writeFileSync(join(tmpdir(), 'legacy-withdraw-witness.json'), JSON.stringify(witnessInput, null, 2));

const { proof, publicSignals } = await fullProve(witnessInput, `${artifactsDir}/withdraw.wasm`, `${artifactsDir}/withdraw.zkey`);
console.log('Proof generated! pubSignals[2] (withdrawnValue):', publicSignals[2]);

const formatted = formatProofForSolidity(proof, publicSignals);

// Simulate first to get clean error
try {
  await publicClient.simulateContract({
    address: entrypointAddress,
    abi: entrypointAbi,
    functionName: 'relay',
    args: [withdrawal, formatted, scope],
    account: walletClient.account,
  });
} catch (e) {
  console.error('Simulation FAILED:', e.shortMessage || e.message);
  console.log(JSON.stringify({ success: false, error: e.shortMessage || e.message }));
  process.exit(0);
}

const relayTx = await walletClient.writeContract({
  address: entrypointAddress,
  abi: entrypointAbi,
  functionName: 'relay',
  args: [withdrawal, formatted, scope],
  gas: 5_000_000n
});

const relayReceipt = await publicClient.waitForTransactionReceipt({ hash: relayTx });

console.log(JSON.stringify({
  success: relayReceipt.status === 'success',
  txHash: relayTx,
  gasUsed: relayReceipt.gasUsed.toString()
}));
