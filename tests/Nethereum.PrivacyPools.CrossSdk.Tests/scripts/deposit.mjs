import {
  generateMasterKeys,
  generateDepositSecrets,
  hashPrecommitment,
  getCommitment
} from '@0xbow/privacy-pools-core-sdk';
import { parseEventLogs } from 'viem';
import { readInput, createClients, entrypointAbi, poolAbi } from './helpers.mjs';

const input = readInput();
const { rpcUrl, chainId, entrypointAddress, poolAddress, privateKey, mnemonic, depositIndex, valueWei, scope: scopeStr } = input;

const { publicClient, walletClient, account } = createClients(rpcUrl, chainId, privateKey);
const keys = generateMasterKeys(mnemonic);
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
    logCount: receipt.logs.length,
    logTopics: receipt.logs.map(l => ({ address: l.address, topics: l.topics }))
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
