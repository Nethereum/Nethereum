import {
  generateMasterKeys,
  generateDepositSecrets,
  hashPrecommitment,
  getCommitment
} from '@0xbow/privacy-pools-core-sdk';
import { parseEventLogs, parseAbi } from 'viem';
import { readInput, createClients, entrypointAbi, poolAbi } from './helpers.mjs';

const input = readInput();
const { rpcUrl, chainId, entrypointAddress, poolAddress, privateKey, mnemonic, depositIndex, valueWei, scope: scopeStr, tokenAddress } = input;

const { publicClient, walletClient, account } = createClients(rpcUrl, chainId, privateKey);
const keys = generateMasterKeys(mnemonic);
const scope = BigInt(scopeStr);

const { nullifier, secret } = generateDepositSecrets(keys, scope, BigInt(depositIndex));
const precommitment = hashPrecommitment(nullifier, secret);

const erc20Abi = parseAbi([
  'function approve(address spender, uint256 amount) returns (bool)'
]);

const balanceAbi = parseAbi(['function balanceOf(address) view returns (uint256)']);
const balance = await publicClient.readContract({
  address: tokenAddress,
  abi: balanceAbi,
  functionName: 'balanceOf',
  args: [account.address]
});
console.error(`Token balance of ${account.address}: ${balance}`);
console.error(`Approving ${valueWei} to ${entrypointAddress}`);

const approveHash = await walletClient.writeContract({
  address: tokenAddress,
  abi: erc20Abi,
  functionName: 'approve',
  args: [entrypointAddress, 2n ** 256n - 1n],
  gas: 100_000n
});
await publicClient.waitForTransactionReceipt({ hash: approveHash });

const depositErc20Abi = [
  {
    type: 'function', name: 'deposit', stateMutability: 'nonpayable',
    inputs: [
      { name: '_asset', type: 'address' },
      { name: '_value', type: 'uint256' },
      { name: '_precommitment', type: 'uint256' }
    ],
    outputs: [{ name: '', type: 'uint256' }]
  }
];

let txHash;
try {
  txHash = await walletClient.writeContract({
    address: entrypointAddress,
    abi: depositErc20Abi,
    functionName: 'deposit',
    args: [tokenAddress, BigInt(valueWei), precommitment],
    gas: 5_000_000n
  });
} catch (e) {
  console.log(JSON.stringify({ error: `writeContract failed: ${e.shortMessage || e.message}`, details: e.details }));
  process.exit(1);
}

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

console.log(JSON.stringify({
  commitmentHash: commitment.hash.toString(),
  label: evt.label.toString(),
  precommitment: precommitment.toString(),
  nullifier: nullifier.toString(),
  secret: secret.toString(),
  value: valueWei,
  scope: scope.toString(),
  txHash,
  blockNumber: receipt.blockNumber.toString(),
  onChainCommitment: evt.commitment.toString()
}));
