import { fullProve } from './snarkjs-cli.mjs';
import {
  generateMasterKeys,
  generateWithdrawalSecrets,
  hashPrecommitment,
  generateMerkleProof,
  calculateContext
} from '@0xbow/privacy-pools-core-sdk';
import { parseEventLogs } from 'viem';
import { readInput, createClients, entrypointAbi, poolAbi, formatProofForSolidity, buildRelayData } from './helpers.mjs';
import { writeFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';

const input = readInput();
const {
  rpcUrl, chainId, entrypointAddress, poolAddress, privateKey, artifactsDir,
  mnemonic, scope,
  existingValue, existingLabel, existingNullifier, existingSecret,
  stateLeaves, stateLeafIndex,
  aspLeaves, aspLeafIndex,
  withdrawnValue, recipientAddress, relayerAddress, relayFeeBps, childIndex
} = input;

const { publicClient, walletClient } = createClients(rpcUrl, chainId, privateKey);

try {

const keys = generateMasterKeys(mnemonic);
const { nullifier: newNullifier, secret: newSecret } = generateWithdrawalSecrets(
  keys, BigInt(existingLabel), BigInt(childIndex ?? '0')
);

const stateLeavesBigInt = stateLeaves.map(l => BigInt(l));
const existingCommitmentHash = stateLeavesBigInt[Number(stateLeafIndex)];
const stateMerkleProof = generateMerkleProof(stateLeavesBigInt, existingCommitmentHash);

const aspLeavesBigInt = aspLeaves.map(l => BigInt(l));
const aspLeaf = aspLeavesBigInt[Number(aspLeafIndex)];
const aspMerkleProof = generateMerkleProof(aspLeavesBigInt, aspLeaf);

const relayData = buildRelayData(recipientAddress, relayerAddress, relayFeeBps || '0');
const withdrawal = {
  processooor: entrypointAddress,
  data: relayData
};
const context = calculateContext(withdrawal, BigInt(scope));

const witnessInput = {
  existingValue: BigInt(existingValue).toString(),
  existingNullifier: BigInt(existingNullifier).toString(),
  existingSecret: BigInt(existingSecret).toString(),
  label: BigInt(existingLabel).toString(),
  newNullifier: newNullifier.toString(),
  newSecret: newSecret.toString(),
  withdrawnValue: BigInt(withdrawnValue).toString(),
  stateRoot: stateMerkleProof.root.toString(),
  stateTreeDepth: BigInt(stateMerkleProof.siblings.filter(s => s !== 0n).length || 1).toString(),
  stateSiblings: stateMerkleProof.siblings.map(s => s.toString()),
  stateIndex: BigInt(stateLeafIndex).toString(),
  ASPRoot: aspMerkleProof.root.toString(),
  ASPTreeDepth: BigInt(aspMerkleProof.siblings.filter(s => s !== 0n).length || 1).toString(),
  ASPSiblings: aspMerkleProof.siblings.map(s => s.toString()),
  ASPIndex: BigInt(aspLeafIndex).toString(),
  context: BigInt(context).toString()
};

const wasmPath = `${artifactsDir}/withdraw.wasm`;
const zkeyPath = `${artifactsDir}/withdraw.zkey`;

writeFileSync(join(tmpdir(), 'withdraw-witness.json'), JSON.stringify(witnessInput, null, 2));
writeFileSync(join(tmpdir(), 'withdraw-debug.json'), JSON.stringify({ witnessInput, publicSignals: null, formatted: null }, null, 2));

let proof, publicSignals;
try {
  ({ proof, publicSignals } = await fullProve(witnessInput, wasmPath, zkeyPath));
} catch (e) {
  console.log(JSON.stringify({
    success: false,
    error: 'fullProve failed: ' + (e.message || e.toString()),
    witnessInput
  }));
  process.exit(0);
}

const formatted = formatProofForSolidity(proof, publicSignals);
writeFileSync(join(tmpdir(), 'withdraw-debug.json'), JSON.stringify({
  publicSignals: publicSignals.map(s => s.toString()),
  formattedPubSignals: formatted.pubSignals.map(s => s.toString()),
  signal2_withdrawnValue: publicSignals[2]?.toString()
}, null, 2));

const { encodeFunctionData } = await import('viem');
const calldata = encodeFunctionData({
  abi: entrypointAbi,
  functionName: 'relay',
  args: [withdrawal, formatted, BigInt(scope)]
});
writeFileSync(join(tmpdir(), 'withdraw-calldata.txt'), calldata);

// Simulate first to catch revert reason
try {
  await publicClient.simulateContract({
    address: entrypointAddress,
    abi: entrypointAbi,
    functionName: 'relay',
    args: [withdrawal, formatted, BigInt(scope)],
    account: walletClient.account,
  });
} catch (simErr) {
  writeFileSync(join(tmpdir(), 'withdraw-sim-error.txt'),
    (simErr.shortMessage || '') + '\n' + (simErr.message || '').slice(0, 2000));
}

let txHash;
try {
  txHash = await walletClient.writeContract({
    address: entrypointAddress,
    abi: entrypointAbi,
    functionName: 'relay',
    args: [withdrawal, formatted, BigInt(scope)],
    gas: 5_000_000n
  });
} catch (e) {
  console.log(JSON.stringify({
    success: false,
    error: e.message || e.toString(),
    shortMessage: e.shortMessage || null
  }));
  process.exit(0);
}

const receipt = await publicClient.waitForTransactionReceipt({ hash: txHash });
writeFileSync(join(tmpdir(), 'withdraw-receipt.json'), JSON.stringify({
  status: receipt.status, gasUsed: receipt.gasUsed?.toString(), logs: receipt.logs.length
}));
if (receipt.status !== 'success') {
  let revertReason = 'unknown';
  try {
    await publicClient.simulateContract({
      address: entrypointAddress,
      abi: entrypointAbi,
      functionName: 'relay',
      args: [withdrawal, formatted, BigInt(scope)],
      account: walletClient.account,
      blockNumber: receipt.blockNumber - 1n
    });
  } catch (simErr) {
    revertReason = simErr.shortMessage || simErr.message || simErr.toString();
  }
  writeFileSync(join(tmpdir(), 'withdraw-revert.txt'), `gasUsed: ${receipt.gasUsed}\nreason: ${revertReason}`);
  console.error('TX REVERTED. See withdraw-revert.txt');
}

const withdrawnLogs = parseEventLogs({
  abi: poolAbi,
  logs: receipt.logs,
  eventName: 'Withdrawn'
});

const newPrecommitment = hashPrecommitment(newNullifier, newSecret);

console.log(JSON.stringify({
  txHash,
  success: receipt.status === 'success',
  blockNumber: receipt.blockNumber.toString(),
  newCommitmentHash: withdrawnLogs.length > 0 ? withdrawnLogs[0].args.newCommitment?.toString() : null,
  spentNullifier: withdrawnLogs.length > 0 ? withdrawnLogs[0].args.spentNullifier?.toString() : null,
  newNullifier: newNullifier.toString(),
  newSecret: newSecret.toString(),
  newPrecommitment: newPrecommitment.toString(),
  proofPublicSignals: publicSignals.map(s => s.toString())
}));

} catch (e) {
  console.log(JSON.stringify({
    success: false,
    error: e.message || e.toString(),
    shortMessage: e.shortMessage || null,
    stack: (e.stack || '').split('\n').slice(0, 5).join('\n')
  }));
}
