package main

import (
	"bytes"
	"context"
	"crypto/ecdsa"
	"encoding/json"
	"fmt"
	"io"
	"math/big"
	"net/http"
	"os"
	"strings"
	"sync"
	"time"

	"github.com/ethereum/go-ethereum"
	"github.com/ethereum/go-ethereum/accounts/abi"
	"github.com/ethereum/go-ethereum/common"
	"github.com/ethereum/go-ethereum/common/math"
	"github.com/ethereum/go-ethereum/core/types"
	"github.com/ethereum/go-ethereum/crypto"
	"github.com/ethereum/go-ethereum/ethclient"
	"github.com/ethereum/go-ethereum/signer/core/apitypes"
	x402 "github.com/x402-foundation/x402/go/v2"
	"github.com/x402-foundation/x402/go/v2/extensions/eip2612gassponsor"
	evmmech "github.com/x402-foundation/x402/go/v2/mechanisms/evm"
	exactevm "github.com/x402-foundation/x402/go/v2/mechanisms/evm/exact/facilitator"
)

const balanceOfABI = `[{"name":"balanceOf","type":"function","inputs":[{"name":"a","type":"address"}],"outputs":[{"name":"","type":"uint256"}]}]`

type signer struct {
	key     *ecdsa.PrivateKey
	address common.Address
	client  *ethclient.Client
	chainID *big.Int
	mu      sync.Mutex
}

func (s *signer) GetAddresses() []string { return []string{s.address.Hex()} }

func (s *signer) GetChainID(context.Context) (*big.Int, error) { return s.chainID, nil }

func (s *signer) GetCode(ctx context.Context, address string) ([]byte, error) {
	return s.client.CodeAt(ctx, common.HexToAddress(address), nil)
}

func (s *signer) GetBalance(ctx context.Context, address string, token string) (*big.Int, error) {
	if token == "" {
		return s.client.BalanceAt(ctx, common.HexToAddress(address), nil)
	}
	out, err := s.ReadContract(ctx, token, []byte(balanceOfABI), "balanceOf", common.HexToAddress(address))
	if err != nil {
		return nil, err
	}
	return out.(*big.Int), nil
}

func (s *signer) ReadContract(ctx context.Context, address string, abiJSON []byte, method string, args ...interface{}) (interface{}, error) {
	parsed, err := abi.JSON(strings.NewReader(string(abiJSON)))
	if err != nil {
		return nil, err
	}
	data, err := parsed.Pack(method, args...)
	if err != nil {
		return nil, err
	}
	to := common.HexToAddress(address)
	result, err := s.client.CallContract(ctx, ethereum.CallMsg{From: s.address, To: &to, Data: data}, nil)
	if err != nil {
		return nil, err
	}
	if len(parsed.Methods[method].Outputs) == 0 {
		return nil, nil
	}
	values, err := parsed.Methods[method].Outputs.Unpack(result)
	if err != nil || len(values) == 0 {
		return nil, err
	}
	return values[0], nil
}

func (s *signer) WriteContract(ctx context.Context, address string, abiJSON []byte, method string, dataSuffix []byte, args ...interface{}) (string, error) {
	parsed, err := abi.JSON(strings.NewReader(string(abiJSON)))
	if err != nil {
		return "", err
	}
	data, err := parsed.Pack(method, args...)
	if err != nil {
		return "", err
	}
	return s.SendTransaction(ctx, address, evmmech.AppendDataSuffix(data, dataSuffix))
}

func (s *signer) SendTransaction(ctx context.Context, to string, data []byte) (string, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	nonce, err := s.client.PendingNonceAt(ctx, s.address)
	if err != nil {
		return "", err
	}
	gasPrice, err := s.client.SuggestGasPrice(ctx)
	if err != nil {
		return "", err
	}
	tx, err := types.SignTx(types.NewTransaction(nonce, common.HexToAddress(to), big.NewInt(0), 300000, gasPrice, data), types.LatestSignerForChainID(s.chainID), s.key)
	if err != nil {
		return "", err
	}
	if err := s.client.SendTransaction(ctx, tx); err != nil {
		return "", err
	}
	return tx.Hash().Hex(), nil
}

func (s *signer) WaitForTransactionReceipt(ctx context.Context, hash string) (*evmmech.TransactionReceipt, error) {
	for i := 0; i < 60; i++ {
		receipt, err := s.client.TransactionReceipt(ctx, common.HexToHash(hash))
		if err == nil && receipt != nil {
			return &evmmech.TransactionReceipt{Status: receipt.Status, BlockNumber: receipt.BlockNumber.Uint64(), TxHash: receipt.TxHash.Hex(), Logs: receipt.Logs}, nil
		}
		time.Sleep(500 * time.Millisecond)
	}
	return nil, fmt.Errorf("receipt not found: %s", hash)
}

func (s *signer) VerifyTypedData(ctx context.Context, address string, domain evmmech.TypedDataDomain, fields map[string][]evmmech.TypedDataField, primaryType string, message map[string]interface{}, signature []byte) (bool, error) {
	data := apitypes.TypedData{
		Types:       apitypes.Types{},
		PrimaryType: primaryType,
		Domain: apitypes.TypedDataDomain{
			Name:              domain.Name,
			Version:           domain.Version,
			ChainId:           (*math.HexOrDecimal256)(domain.ChainID),
			VerifyingContract: domain.VerifyingContract,
		},
		Message: message,
	}
	for name, list := range fields {
		for _, field := range list {
			data.Types[name] = append(data.Types[name], apitypes.Type{Name: field.Name, Type: field.Type})
		}
	}
	var domainFields []apitypes.Type
	if data.Domain.Name != "" {
		domainFields = append(domainFields, apitypes.Type{Name: "name", Type: "string"})
	}
	if data.Domain.Version != "" {
		domainFields = append(domainFields, apitypes.Type{Name: "version", Type: "string"})
	}
	if data.Domain.ChainId != nil {
		domainFields = append(domainFields, apitypes.Type{Name: "chainId", Type: "uint256"})
	}
	if data.Domain.VerifyingContract != "" {
		domainFields = append(domainFields, apitypes.Type{Name: "verifyingContract", Type: "address"})
	}
	data.Types["EIP712Domain"] = domainFields
	messageHash, err := data.HashStruct(primaryType, message)
	if err != nil {
		return false, err
	}
	domainHash, err := data.HashStruct("EIP712Domain", data.Domain.Map())
	if err != nil {
		return false, err
	}
	if len(signature) != 65 {
		return false, fmt.Errorf("invalid signature length: %d", len(signature))
	}
	sig := append([]byte{}, signature...)
	if sig[64] >= 27 {
		sig[64] -= 27
	}
	pub, err := crypto.SigToPub(crypto.Keccak256(append(append([]byte{0x19, 0x01}, domainHash...), messageHash...)), sig)
	if err != nil {
		return false, err
	}
	return bytes.Equal(crypto.PubkeyToAddress(*pub).Bytes(), common.HexToAddress(address).Bytes()), nil
}

func main() {
	port, rpc, key := os.Args[1], os.Args[2], os.Args[3]
	privateKey, err := crypto.HexToECDSA(strings.TrimPrefix(key, "0x"))
	if err != nil {
		panic(err)
	}
	client, err := ethclient.Dial(rpc)
	if err != nil {
		panic(err)
	}
	chainID, err := client.ChainID(context.Background())
	if err != nil {
		panic(err)
	}
	s := &signer{key: privateKey, address: crypto.PubkeyToAddress(privateKey.PublicKey), client: client, chainID: chainID}
	facilitator := x402.Newx402Facilitator()
	facilitator.Register([]x402.Network{x402.Network("eip155:" + chainID.String())}, exactevm.NewExactEvmScheme(s, &exactevm.ExactEvmSchemeConfig{}))
	facilitator.RegisterExtension(eip2612gassponsor.EIP2612GasSponsoring)

	reply := func(w http.ResponseWriter, result any, err error) {
		w.Header().Set("Content-Type", "application/json")
		if err != nil {
			w.WriteHeader(http.StatusInternalServerError)
			result = map[string]string{"error": err.Error()}
		}
		json.NewEncoder(w).Encode(result)
	}
	request := func(r *http.Request) ([]byte, []byte) {
		body, _ := io.ReadAll(r.Body)
		var parsed struct {
			PaymentPayload      json.RawMessage `json:"paymentPayload"`
			PaymentRequirements json.RawMessage `json:"paymentRequirements"`
		}
		json.Unmarshal(body, &parsed)
		return parsed.PaymentPayload, parsed.PaymentRequirements
	}
	mux := http.NewServeMux()
	mux.HandleFunc("/supported", func(w http.ResponseWriter, r *http.Request) {
		reply(w, facilitator.GetSupported(), nil)
	})
	mux.HandleFunc("/verify", func(w http.ResponseWriter, r *http.Request) {
		payload, requirements := request(r)
		result, err := facilitator.Verify(r.Context(), payload, requirements)
		reply(w, result, err)
	})
	mux.HandleFunc("/settle", func(w http.ResponseWriter, r *http.Request) {
		payload, requirements := request(r)
		result, err := facilitator.Settle(r.Context(), payload, requirements)
		reply(w, result, err)
	})
	fmt.Println("listening")
	panic(http.ListenAndServe("127.0.0.1:"+port, mux))
}
