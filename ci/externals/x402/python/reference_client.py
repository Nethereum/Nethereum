import asyncio
import json
import sys

import httpx
from eth_account import Account
from x402 import x402Client
from x402.http import decode_payment_response_header
from x402.http.clients import x402_httpx_transport
from x402.mechanisms.evm import EthAccountSignerWithRPC
from x402.mechanisms.evm.exact import register_exact_evm_client


async def main(rpc, payer_key, url):
    client = x402Client()
    register_exact_evm_client(client, EthAccountSignerWithRPC(Account.from_key(payer_key), rpc_url=rpc))
    async with httpx.AsyncClient(transport=x402_httpx_transport(client), timeout=120) as http:
        response = await http.get(url)
    header = response.headers.get("PAYMENT-RESPONSE")
    settle = decode_payment_response_header(header).model_dump() if header else None
    print(json.dumps({"status": response.status_code, "body": response.text, "settle": settle}))


asyncio.run(main(*sys.argv[1:4]))
