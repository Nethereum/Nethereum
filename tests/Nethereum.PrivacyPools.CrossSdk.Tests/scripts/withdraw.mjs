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

const { proof, publicSignals } = await fullProve(witnessInput, wasmPath, zkeyPath);

const formatted = formatProofForSolidity(proof, publicSignals);

const txHash = await walletClient.writeContract({
  address: entrypointAddress,
  abi: entrypointAbi,
  functionName: 'relay',
  args: [withdrawal, formatted, BigInt(scope)],
  gas: 5_000_000n
});

const receipt = await publicClient.waitForTransactionReceipt({ hash: txHash });

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
  newCommitmentHash: withdrawnLogs.length > 0 ? withdrawnLogs[0].args.newCommitment.toString() : null,
  spentNullifier: withdrawnLogs.length > 0 ? withdrawnLogs[0].args.spentNullifier.toString() : null,
  newNullifier: newNullifier.toString(),
  newSecret: newSecret.toString(),
  newPrecommitment: newPrecommitment.toString(),
  proofPublicSignals: publicSignals.map(s => s.toString())
}));
