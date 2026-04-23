import {
  generateDepositSecrets,
  hashPrecommitment,
  getCommitment
} from '@0xbow/privacy-pools-core-sdk';
import { mnemonicToAccount } from 'viem/accounts';
import { poseidon } from 'maci-crypto/build/ts/hashing.js';
import { parseEventLogs } from 'viem';
import { readInput, createClients, entrypointAbi, poolAbi } from './helpers.mjs';

// Replicates old viem bytesToNumber: IEEE 754 double truncation (53-bit mantissa)
function bytesToNumberLossy(bytes) {
  let value = 0;
  for (let i = 0; i < bytes.length; i++) {
    value = value * 256 + bytes[i];
  }
  return value;
}

const input = readInput();
const { rpcUrl, chainId, entrypointAddress, poolAddress, privateKey, mnemonic, depositIndex, valueWei, scope: scopeStr } = input;

const { publicClient, walletClient, account } = createClients(rpcUrl, chainId, privateKey);

// Legacy key derivation using double-precision truncation (53-bit lossy)
const key1 = bytesToNumberLossy(
  mnemonicToAccount(mnemonic, { accountIndex: 0 }).getHdKey().privateKey
);
const key2 = bytesToNumberLossy(
  mnemonicToAccount(mnemonic, { accountIndex: 1 }).getHdKey().privateKey
);
const masterNullifier = poseidon([BigInt(key1)]);
const masterSecret = poseidon([BigInt(key2)]);
const keys = { masterNullifier, masterSecret };

const scope = BigInt(scopeStr);
const { nullifier, secret } = generateDepositSecrets(keys, scope, BigInt(depositIndex));
const precommitment = hashPrecommitment(nullifier, secret);

const txHash = await walletClient.writeContract({
  address: entrypointAddress,
  abi: entrypointAbi,
  functionName: 'deposit',
  args: [precommitment],
  value: BigInt(valueWei),
  gas: 5_000_000n
});

const receipt = await publicClient.waitForTransactionReceipt({ hash: txHash });

const poolLogs = parseEventLogs({
  abi: poolAbi,
  logs: receipt.logs,
  eventName: 'Deposited'
});

if (poolLogs.length === 0) {
  console.log(JSON.stringify({
    error: 'No Deposited event found',
    receiptStatus: receipt.status,
    logCount: receipt.logs.length
  }));
  process.exit(1);
}

const evt = poolLogs[0].args;
const commitment = getCommitment(BigInt(valueWei), evt.label, nullifier, secret);

const result = {
  commitmentHash: commitment.hash.toString(),
  label: evt.label.toString(),
  precommitment: precommitment.toString(),
  nullifier: nullifier.toString(),
  secret: secret.toString(),
  value: valueWei,
  scope: scope.toString(),
  masterNullifier: keys.masterNullifier.toString(),
  masterSecret: keys.masterSecret.toString(),
  txHash: txHash,
  blockNumber: receipt.blockNumber.toString(),
  onChainCommitment: evt.commitment.toString()
};

console.log(JSON.stringify(result));
