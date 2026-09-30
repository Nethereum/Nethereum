// filtermaps-vector-gen emits ground-truth (value,mapIndex,layer,lvIndex) ->
// (maskedMapIndex,rowIndex,columnIndex) vectors for the Nethereum.Freezer
// filtermaps conformance suite (impl-plan Task 1, spec §C2 checklist #9).
//
// The four hashing functions below are copied VERBATIM from go-ethereum
// core/filtermaps/math.go @ bbb9119caaefbbf619363493e54cc8362d65188d
// (the only change is common.Hash -> [32]byte, common.Address -> [20]byte,
// which are exactly those aliases in geth). Because geth's math_test.go has no
// hardcoded golden vectors (round-trip/statistical only), these vectors are
// EMITTED from geth's real code — the only valid byte-parity ground truth.
//
// Run: go run . > ../../tests/Nethereum.Freezer.UnitTests/Fixtures/filtermaps-vectors.json
package main

import (
	"crypto/sha256"
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	"hash/fnv"
	"math"
	"os"
)

type Hash [32]byte
type Address [20]byte

// --- verbatim geth Params (log* + ratio set by DefaultParams; derived fields computed below) ---
type Params struct {
	logMapHeight       uint
	logMapWidth        uint
	logMapsPerEpoch    uint
	logValuesPerMap    uint
	baseRowLengthRatio uint
	logLayerDiff       uint

	mapHeight        uint32
	mapsPerEpoch     uint32
	valuesPerMap     uint64
	baseRowLength    uint32
	baseRowGroupSize uint32
}

func defaultParams() Params {
	p := Params{
		logMapHeight:       16,
		logMapWidth:        24,
		logMapsPerEpoch:    10,
		logValuesPerMap:    16,
		baseRowGroupSize:   32,
		baseRowLengthRatio: 8,
		logLayerDiff:       4,
	}
	// geth deriveFields (powers of two): mapHeight/mapsPerEpoch/valuesPerMap/baseRowLength.
	p.mapHeight = uint32(1) << p.logMapHeight
	p.mapsPerEpoch = uint32(1) << p.logMapsPerEpoch
	p.valuesPerMap = uint64(1) << p.logValuesPerMap
	p.baseRowLength = uint32(p.valuesPerMap * uint64(p.baseRowLengthRatio) / uint64(p.mapHeight))
	return p
}

// --- verbatim geth core/filtermaps/math.go @ bbb9119c ---

func (p *Params) maxRowLength(layerIndex uint32) uint32 {
	logLayerDiff := uint(layerIndex) * p.logLayerDiff
	if logLayerDiff > p.logMapsPerEpoch {
		logLayerDiff = p.logMapsPerEpoch
	}
	return p.baseRowLength << logLayerDiff
}

func (p *Params) rowIndex(mapIndex, layerIndex uint32, logValue Hash) uint32 {
	hasher := sha256.New()
	hasher.Write(logValue[:])
	var indexEnc [8]byte
	binary.LittleEndian.PutUint32(indexEnc[0:4], p.maskedMapIndex(mapIndex, layerIndex))
	binary.LittleEndian.PutUint32(indexEnc[4:8], layerIndex)
	hasher.Write(indexEnc[:])
	var hash Hash
	hasher.Sum(hash[:0])
	return binary.LittleEndian.Uint32(hash[:4]) % p.mapHeight
}

func (p *Params) columnIndex(lvIndex uint64, logValue *Hash) uint32 {
	var indexEnc [8]byte
	binary.LittleEndian.PutUint64(indexEnc[:], lvIndex)
	hasher := fnv.New64a()
	hasher.Write(indexEnc[:])
	hasher.Write(logValue[:])
	hash := hasher.Sum64()
	hashBits := p.logMapWidth - p.logValuesPerMap
	return uint32(lvIndex%p.valuesPerMap)<<hashBits + (uint32(hash>>(64-hashBits)) ^ uint32(hash)>>(32-hashBits))
}

func (p *Params) maskedMapIndex(mapIndex, layerIndex uint32) uint32 {
	logLayerDiff := uint(layerIndex) * p.logLayerDiff
	if logLayerDiff > p.logMapsPerEpoch {
		logLayerDiff = p.logMapsPerEpoch
	}
	return mapIndex & (uint32(math.MaxUint32) << (p.logMapsPerEpoch - logLayerDiff))
}

func addressValue(address Address) Hash {
	var result Hash
	hasher := sha256.New()
	hasher.Write(address[:])
	hasher.Sum(result[:0])
	return result
}

func topicValue(topic Hash) Hash {
	var result Hash
	hasher := sha256.New()
	hasher.Write(topic[:])
	hasher.Sum(result[:0])
	return result
}

// --- harness ---

type vector struct {
	ValueKind      string `json:"valueKind"`      // "address" | "topic"
	Input          string `json:"input"`          // hex of the address(20)/topic(32) preimage
	Value          string `json:"value"`          // hex of the 32-byte SHA256 value hash
	MapIndex       uint32 `json:"mapIndex"`
	Layer          uint32 `json:"layer"`
	LvIndex        uint64 `json:"lvIndex"`
	MaskedMapIndex uint32 `json:"maskedMapIndex"`
	RowIndex       uint32 `json:"rowIndex"`
	ColumnIndex    uint32 `json:"columnIndex"`
}

func addr(b byte) Address { var a Address; for i := range a { a[i] = b + byte(i) }; return a }
func topic(b byte) Hash   { var h Hash; for i := range h { h[i] = b + byte(i) }; return h }

func main() {
	p := defaultParams()

	type src struct {
		kind  string
		input []byte
		value Hash
	}
	a1, a2 := addr(0x01), addr(0xa0)
	t1, t2 := topic(0x00), topic(0x80)
	srcs := []src{
		{"address", a1[:], addressValue(a1)},
		{"address", a2[:], addressValue(a2)},
		{"topic", t1[:], topicValue(t1)},
		{"topic", t2[:], topicValue(t2)},
	}

	// Cover layers 0-4; two mapIndexes in the SAME epoch (5 and 1000, both < 1024)
	// so a layer-0 vector proves constant-per-epoch masking; and a spread of lvIndexes
	// (incl. across a valuesPerMap=65536 boundary) so columnIndex's fold is exercised.
	mapIdxs := []uint32{5, 1000, 2048, 100000}
	layers := []uint32{0, 1, 2, 3, 4}
	lvIdxs := []uint64{0, 1, 100, 65535, 65536, 1000000}

	var out []vector
	for _, s := range srcs {
		lv := s.value
		for _, mi := range mapIdxs {
			for _, ly := range layers {
				vi := lvIdxs[int(mi+ly)%len(lvIdxs)] // vary lvIndex per case deterministically
				out = append(out, vector{
					ValueKind:      s.kind,
					Input:          hex.EncodeToString(s.input),
					Value:          hex.EncodeToString(lv[:]),
					MapIndex:       mi,
					Layer:          ly,
					LvIndex:        vi,
					MaskedMapIndex: p.maskedMapIndex(mi, ly),
					RowIndex:       p.rowIndex(mi, ly, lv),
					ColumnIndex:    p.columnIndex(vi, &lv),
				})
			}
		}
	}

	enc := json.NewEncoder(os.Stdout)
	enc.SetIndent("", "  ")
	if err := enc.Encode(out); err != nil {
		panic(err)
	}
}
