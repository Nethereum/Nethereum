Light client verification requires deterministic fixtures. Use the following sources to populate this folder (never commit upstream copyrighted blobs without checking licenses).

## Layout

- `bls/` – aggregate + single signature vectors from the Ethereum Foundation consensus-spec-tests repo (`tests/general/BLS/*`). These are derived from the [ethereum/consensus-spec-tests](https://github.com/ethereum/consensus-spec-tests) repository.
- `ssz/py-ssz/light_client_vectors.json` – local test vectors generated via `tests/LightClientVectors/ssz/generate_py_ssz_vectors.py` (this script consumes the sibling `py-ssz` repo). Before running it:
  - `pip install -r tests/LightClientVectors/ssz/requirements.txt`
  - `pip install -e ../py-ssz` (from the repo root, so the adjacent `py-ssz` project is available as a package)
- `ssz/consensus-spec-tests/**` – canonical SSZ fixtures copied from the sibling `consensus-spec-tests` repo (`tests/mainnet/<fork>/ssz_static`). Copy only the types we depend on (BeaconBlockHeader, ExecutionPayloadHeader, SyncCommittee, SyncAggregate, LightClientBootstrap/Update, etc.) to limit repository size.
- `proofs/` – `eth_getProof` JSON blobs captured from devnets or mainnet to drive Patricia verification.
- `traces/` – archived execution traces (JSON) used to compare `IVerifiedEvmExecutor` vs `eth_call`.

## Recommended sources

- Clone `https://github.com/ethereum/consensus-spec-tests` and copy the relevant `general/phase0/bls` and `mainnet/{deneb,electra}/ssz_static/light_client` test cases.
- Use [Ethereum Foundation BLS12-381 test vectors](https://github.com/ethereum/bls12-381-tests) for additional coverage.
- For future `proofs/` fixtures, capture responses from `eth_getProof` against a trusted execution RPC endpoint and strip private data before committing.

Document the origin (commit hash, network, block number) for each fixture inside an adjacent `.md` note to keep provenance clear.
