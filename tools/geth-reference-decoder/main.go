package main

import (
	"bufio"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os"
	"strings"

	"github.com/ethereum/go-ethereum/version"
)

type request struct {
	ID   int64  `json:"id"`
	Kind string `json:"kind"`
	Hex  string `json:"hex"`
}

type response struct {
	ID        int64          `json:"id"`
	OK        bool           `json:"ok"`
	Err       string         `json:"err,omitempty"`
	Canonical string         `json:"canonical,omitempty"`
	Hash      string         `json:"hash,omitempty"`
	Extra     map[string]any `json:"extra,omitempty"`
}

func main() {
	if version.Major != 1 || version.Minor != 17 || version.Patch != 4 {
		fmt.Fprintf(os.Stderr, "geth-reference-decoder requires go-ethereum 1.17.4, built against %d.%d.%d\n", version.Major, version.Minor, version.Patch)
		os.Exit(2)
	}
	if len(os.Args) > 1 && os.Args[1] == "--kinds" {
		for _, name := range kindNames() {
			fmt.Println(name)
		}
		return
	}
	reader := bufio.NewReaderSize(os.Stdin, 1<<20)
	writer := bufio.NewWriterSize(os.Stdout, 1<<20)
	defer writer.Flush()
	encoder := json.NewEncoder(writer)
	for {
		line, err := reader.ReadBytes('\n')
		if len(line) > 0 {
			encoder.Encode(handle(line))
			if reader.Buffered() == 0 {
				writer.Flush()
			}
		}
		if err != nil {
			return
		}
	}
}

func handle(line []byte) (out response) {
	var req request
	if err := json.Unmarshal(line, &req); err != nil {
		return response{Err: "request: " + err.Error()}
	}
	out.ID = req.ID
	defer func() {
		if r := recover(); r != nil {
			out = response{ID: req.ID, Err: fmt.Sprintf("panic: %v", r)}
		}
	}()
	fn, ok := kinds[req.Kind]
	if !ok {
		return response{ID: req.ID, Err: "unknown kind " + req.Kind}
	}
	data, err := hex.DecodeString(strings.TrimPrefix(req.Hex, "0x"))
	if err != nil {
		return response{ID: req.ID, Err: "hex: " + err.Error()}
	}
	res := fn(data)
	res.ID = req.ID
	return res
}
