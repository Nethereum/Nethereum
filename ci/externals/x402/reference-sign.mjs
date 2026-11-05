import { privateKeyToAccount } from "viem/accounts";
import { createPublicClient, http } from "viem";

const [rpc, payerKey, reqJson, version] = process.argv.slice(2);
const suffix = version === "2.10.0" ? "-2-10" : "";
const { toClientEvmSigner } = await import(`@x402/evm${suffix}`);
const { ExactEvmScheme } = await import(`@x402/evm${suffix}/exact/client`);

const requirements = JSON.parse(reqJson);
const account = privateKeyToAccount(payerKey);
const publicClient = createPublicClient({ transport: http(rpc) });
const signer = toClientEvmSigner(account, publicClient);
const scheme = new ExactEvmScheme(signer);
const result = await scheme.createPaymentPayload(2, requirements);
console.log(JSON.stringify({ x402Version: 2, accepted: requirements, payload: result.payload }));
