package main

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"

	"github.com/ethereum/go-ethereum/ethclient"
	x402 "github.com/x402-foundation/x402/go/v2"
	x402http "github.com/x402-foundation/x402/go/v2/http"
	exactevm "github.com/x402-foundation/x402/go/v2/mechanisms/evm/exact/client"
	evmsigners "github.com/x402-foundation/x402/go/v2/signers/evm"
)

func main() {
	rpc, payerKey, url := os.Args[1], os.Args[2], os.Args[3]
	ethClient, err := ethclient.Dial(rpc)
	if err != nil {
		panic(err)
	}
	signer, err := evmsigners.NewClientSignerFromPrivateKeyWithClient(payerKey, ethClient)
	if err != nil {
		panic(err)
	}
	client := x402.Newx402Client().
		Register("eip155:*", exactevm.NewExactEvmScheme(signer, &exactevm.ExactEvmSchemeConfig{RPCURL: rpc}))
	httpClient := x402http.Newx402HTTPClient(client)
	response, err := x402http.WrapHTTPClientWithPayment(http.DefaultClient, httpClient).Get(url)
	if err != nil {
		panic(err)
	}
	defer response.Body.Close()
	body, _ := io.ReadAll(response.Body)
	headers := map[string]string{}
	for name := range response.Header {
		headers[name] = response.Header.Get(name)
	}
	settle, _ := httpClient.GetPaymentSettleResponse(headers)
	out, _ := json.Marshal(map[string]any{"status": response.StatusCode, "body": string(body), "settle": settle})
	fmt.Println(string(out))
}
