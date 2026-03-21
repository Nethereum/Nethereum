import { createPublicClient, createWalletClient, http, parseEventLogs, encodeAbiParameters } from 'viem';
import { privateKeyToAccount } from 'viem/accounts';
import { readFileSync } from 'fs';

export function readInput() {
  return JSON.parse(readFileSync(process.argv[2], 'utf8'));
}

export function createClients(rpcUrl, chainId, privateKey) {
  const chain = {
    id: chainId,
    name: 'DevChain',
    nativeCurrency: { name: 'Ether', symbol: 'ETH', decimals: 18 },
    rpcUrls: { default: { http: [rpcUrl] } }
  };
  const transport = http(rpcUrl);
  const account = privateKeyToAccount(privateKey);
  const publicClient = createPublicClient({ chain, transport });
  const walletClient = createWalletClient({ chain, transport, account });
  return { publicClient, walletClient, account, chain };
}

export const entrypointAbi = [
  {
    type: 'function', name: 'deposit', stateMutability: 'payable',
    inputs: [{ name: 'precommitment', type: 'uint256' }],
    outputs: []
  },
  {
    type: 'function', name: 'relay', stateMutability: 'nonpayable',
    inputs: [
      {
        name: 'withdrawal', type: 'tuple',
        components: [
          { name: 'processooor', type: 'address' },
          { name: 'data', type: 'bytes' }
        ]
      },
      {
        name: 'proof', type: 'tuple',
        components: [
          { name: 'pA', type: 'uint256[2]' },
          { name: 'pB', type: 'uint256[2][2]' },
          { name: 'pC', type: 'uint256[2]' },
          { name: 'pubSignals', type: 'uint256[8]' }
        ]
      },
      { name: 'scope', type: 'uint256' }
    ],
    outputs: []
  },
  {
    type: 'function', name: 'updateRoot', stateMutability: 'nonpayable',
    inputs: [
      { name: 'root', type: 'uint256' },
      { name: 'ipfsCID', type: 'string' }
    ],
    outputs: []
  },
  {
    type: 'function', name: 'latestRoot', stateMutability: 'view',
    inputs: [],
    outputs: [{ name: '', type: 'uint256' }]
  }
];

export const poolAbi = [
  {
    type: 'function', name: 'scope', stateMutability: 'view',
    inputs: [],
    outputs: [{ name: '', type: 'uint256' }]
  },
  {
    type: 'function', name: 'currentRoot', stateMutability: 'view',
    inputs: [],
    outputs: [{ name: '', type: 'uint256' }]
  },
  {
    type: 'function', name: 'currentTreeSize', stateMutability: 'view',
    inputs: [],
    outputs: [{ name: '', type: 'uint256' }]
  },
  {
    type: 'function', name: 'nullifierHashes', stateMutability: 'view',
    inputs: [{ name: '', type: 'uint256' }],
    outputs: [{ name: '', type: 'bool' }]
  },
  {
    type: 'function', name: 'ragequit', stateMutability: 'nonpayable',
    inputs: [
      {
        name: 'proof', type: 'tuple',
        components: [
          { name: 'pA', type: 'uint256[2]' },
          { name: 'pB', type: 'uint256[2][2]' },
          { name: 'pC', type: 'uint256[2]' },
          { name: 'pubSignals', type: 'uint256[4]' }
        ]
      }
    ],
    outputs: []
  },
  {
    type: 'event', name: 'Deposited',
    inputs: [
      { name: 'depositor', type: 'address', indexed: true },
      { name: 'commitment', type: 'uint256', indexed: false },
      { name: 'label', type: 'uint256', indexed: false },
      { name: 'value', type: 'uint256', indexed: false },
      { name: 'precommitment', type: 'uint256', indexed: false }
    ]
  },
  {
    type: 'event', name: 'Withdrawn',
    inputs: [
      { name: 'spentNullifier', type: 'uint256', indexed: true },
      { name: 'newCommitment', type: 'uint256', indexed: true },
      { name: 'value', type: 'uint256', indexed: false }
    ]
  },
  {
    type: 'event', name: 'Ragequit',
    inputs: [
      { name: 'ragequitter', type: 'address', indexed: true },
      { name: 'commitment', type: 'uint256', indexed: true },
      { name: 'label', type: 'uint256', indexed: true },
      { name: 'value', type: 'uint256', indexed: false }
    ]
  }
];

export function formatProofForSolidity(proof, publicSignals) {
  const toBigInt = (v) => BigInt(v);
  return {
    pA: [toBigInt(proof.pi_a[0]), toBigInt(proof.pi_a[1])],
    pB: [
      [toBigInt(proof.pi_b[0][1]), toBigInt(proof.pi_b[0][0])],
      [toBigInt(proof.pi_b[1][1]), toBigInt(proof.pi_b[1][0])]
    ],
    pC: [toBigInt(proof.pi_c[0]), toBigInt(proof.pi_c[1])],
    pubSignals: publicSignals.map(s => toBigInt(s))
  };
}

export function buildRelayData(recipientAddress, relayerAddress, relayFeeBps) {
  return encodeAbiParameters(
    [
      { name: 'recipient', type: 'address' },
      { name: 'relayer', type: 'address' },
      { name: 'relayFeeBps', type: 'uint256' }
    ],
    [recipientAddress, relayerAddress, BigInt(relayFeeBps)]
  );
}
