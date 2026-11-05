import { privateKeyToAccount } from "viem/accounts";
import { createPublicClient, http } from "viem";

const [version, rpc, payerKey, url] = process.argv.slice(2);
const suffix = version === "2.10.0" ? "-2-10" : "";
const { x402Client, x402HTTPClient } = await import(`@x402/core${suffix}/client`);
const { toClientEvmSigner } = await import(`@x402/evm${suffix}`);
const { ExactEvmScheme } = await import(`@x402/evm${suffix}/exact/client`);
const { wrapFetchWithPayment } = await import(`@x402/fetch${suffix}`);

const signer = toClientEvmSigner(privateKeyToAccount(payerKey), createPublicClient({ transport: http(rpc) }));
const client = new x402Client().register("eip155:*", new ExactEvmScheme(signer, { rpcUrl: rpc }));
const response = await wrapFetchWithPayment(fetch, client)(url);
let settle = null;
try {
  settle = new x402HTTPClient(client).getPaymentSettleResponse((name) => response.headers.get(name));
} catch {}
console.log(JSON.stringify({ status: response.status, body: await response.text(), settle }));
