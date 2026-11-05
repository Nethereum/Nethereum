import http from "node:http";
import { privateKeyToAccount } from "viem/accounts";
import { createWalletClient, http as viemHttp, publicActions, parseTransaction, recoverTransactionAddress } from "viem";
import { baseSepolia } from "viem/chains";
import { x402Facilitator } from "@x402/core/facilitator";
import { toFacilitatorEvmSigner } from "@x402/evm";
import { ExactEvmScheme } from "@x402/evm/exact/facilitator";
import { EIP2612_GAS_SPONSORING, createErc20ApprovalGasSponsoringExtension } from "@x402/extensions";

const [port, rpc, facilitatorKey] = process.argv.slice(2);
const account = privateKeyToAccount(facilitatorKey);
const viemClient = createWalletClient({ account, chain: baseSepolia, transport: viemHttp(rpc) }).extend(publicActions);

const signer = toFacilitatorEvmSigner({
  address: account.address,
  readContract: (args) => viemClient.readContract({ ...args, args: args.args || [] }),
  verifyTypedData: (args) => viemClient.verifyTypedData(args),
  writeContract: (args) => viemClient.writeContract({ ...args, args: args.args || [] }),
  sendTransaction: (args) => viemClient.sendTransaction(args),
  waitForTransactionReceipt: (args) => viemClient.waitForTransactionReceipt(args),
  getCode: (args) => viemClient.getCode(args),
  simulateCalls: (args) => viemClient.simulateCalls(args),
});

const sendTransactions = async (transactions) => {
  const hashes = [];
  for (const tx of transactions) {
    let hash;
    if (typeof tx === "string") {
      const payer = await recoverTransactionAddress({ serializedTransaction: tx });
      const parsed = parseTransaction(tx);
      const gasCost = (parsed.gas ?? 70_000n) * (parsed.maxFeePerGas ?? 1_000_000_000n);
      const balance = await viemClient.getBalance({ address: payer });
      if (balance < gasCost) {
        const fund = await viemClient.sendTransaction({ to: payer, value: gasCost - balance });
        await viemClient.waitForTransactionReceipt({ hash: fund });
      }
      hash = await viemClient.sendRawTransaction({ serializedTransaction: tx });
    } else {
      hash = await viemClient.sendTransaction(tx);
    }
    const receipt = await viemClient.waitForTransactionReceipt({ hash });
    if (receipt.status !== "success") throw new Error(`transaction_failed: ${hash}`);
    hashes.push(hash);
  }
  return hashes;
};

const facilitator = new x402Facilitator()
  .register("eip155:84532", new ExactEvmScheme(signer))
  .registerExtension(EIP2612_GAS_SPONSORING)
  .registerExtension(createErc20ApprovalGasSponsoringExtension({ ...signer, sendTransactions }));

const readBody = async (request) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return JSON.parse(Buffer.concat(chunks).toString());
};

http.createServer(async (request, response) => {
  try {
    let result;
    if (request.method === "GET" && request.url === "/supported") {
      result = facilitator.getSupported();
    } else {
      const { paymentPayload, paymentRequirements } = await readBody(request);
      result = request.url === "/verify"
        ? await facilitator.verify(paymentPayload, paymentRequirements)
        : await facilitator.settle(paymentPayload, paymentRequirements);
    }
    response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify(result));
  } catch (error) {
    response.writeHead(500, { "content-type": "application/json" }).end(JSON.stringify({ error: String(error?.message ?? error) }));
  }
}).listen(Number(port), "127.0.0.1", () => console.log("listening"));
