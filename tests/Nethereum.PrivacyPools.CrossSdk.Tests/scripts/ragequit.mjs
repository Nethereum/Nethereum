import { fullProve } from './snarkjs-cli.mjs';
import { readInput, createClients, poolAbi, formatProofForSolidity } from './helpers.mjs';

const input = readInput();
const { rpcUrl, chainId, poolAddress, privateKey, artifactsDir, value, label, nullifier, secret } = input;

const { publicClient, walletClient } = createClients(rpcUrl, chainId, privateKey);

const witnessInput = {
  value: BigInt(value).toString(),
  label: BigInt(label).toString(),
  nullifier: BigInt(nullifier).toString(),
  secret: BigInt(secret).toString()
};

const wasmPath = `${artifactsDir}/commitment.wasm`;
const zkeyPath = `${artifactsDir}/commitment.zkey`;

const { proof, publicSignals } = await fullProve(witnessInput, wasmPath, zkeyPath);

const formatted = formatProofForSolidity(proof, publicSignals);

const txHash = await walletClient.writeContract({
  address: poolAddress,
  abi: poolAbi,
  functionName: 'ragequit',
  args: [formatted],
  gas: 5_000_000n
});

const receipt = await publicClient.waitForTransactionReceipt({ hash: txHash });

console.log(JSON.stringify({
  txHash,
  success: receipt.status === 'success',
  blockNumber: receipt.blockNumber.toString(),
  proofPublicSignals: publicSignals.map(s => s.toString())
}));
