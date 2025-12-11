#!/usr/bin/env python3
"""
Generate SSZ encode + hash_tree_root vectors using the upstream py-ssz library.

Prerequisites (run inside a Python environment with internet access):
    pip install -r tests/LightClientVectors/ssz/requirements.txt
    pip install -e ../py-ssz   (run from the repo root so the sibling py-ssz is available)

Usage:
    python tests/LightClientVectors/ssz/generate_py_ssz_vectors.py
"""

from __future__ import annotations

import json
import sys
from collections import OrderedDict
from pathlib import Path
from typing import Any, Dict

try:
    import types

    from lru import LRU  # type: ignore
except ImportError:
    # Minimal fallback so py-ssz can initialize without the optional lru dependency.
    class LRU(OrderedDict):  # type: ignore
        def __init__(self, capacity: int):
            super().__init__()
            self.capacity = capacity

        def __getitem__(self, key: Any) -> Any:
            value = super().__getitem__(key)
            self.move_to_end(key)
            return value

        def __setitem__(self, key: Any, value: Any) -> None:
            if key in self:
                super().__setitem__(key, value)
                self.move_to_end(key)
            else:
                super().__setitem__(key, value)
            if len(self) > self.capacity:
                self.popitem(last=False)

    lru_module = types.ModuleType("lru")
    setattr(lru_module, "LRU", LRU)
    sys.modules["lru"] = lru_module

# Locate the sibling py-ssz repository.
repo_root = Path(__file__).resolve().parents[3]
py_ssz_repo = repo_root.parent / "py-ssz"
if not py_ssz_repo.exists():
    sys.exit(f"py-ssz repository not found at {py_ssz_repo}. Clone it next to the Nethereum folder.")

sys.path.append(str(py_ssz_repo))

try:
    import ssz  # type: ignore
    from ssz import Serializable  # type: ignore
    from ssz.sedes import ByteList, ByteVector, List, Vector, uint64  # type: ignore
except ImportError as exc:  # pragma: no cover - for manual script execution
    sys.exit(f"Unable to import py-ssz or its dependencies: {exc}")

# Some distro builds of py-ssz don't set ByteList.length. Patch it to fall back to max_length.
if not hasattr(ByteList, "length"):  # type: ignore
    setattr(ByteList, "length", property(lambda self: getattr(self, "_length", self.max_length)))  # type: ignore

_byte_list_orig_serialize = ByteList.serialize


def _patched_byte_list_serialize(self, value):
    if isinstance(value, (tuple, list)):
        value = b"".join(value)
    return _byte_list_orig_serialize(self, value)


ByteList.serialize = _patched_byte_list_serialize  # type: ignore


def bytes_seq(length: int, seed: int) -> bytes:
    return bytes((seed + i) % 256 for i in range(length))


class BeaconBlockHeader(Serializable):
    fields = (
        ("slot", uint64),
        ("proposer_index", uint64),
        ("parent_root", ByteVector(32)),
        ("state_root", ByteVector(32)),
        ("body_root", ByteVector(32)),
    )


class ExecutionPayloadHeader(Serializable):
    fields = (
        ("parent_hash", ByteVector(32)),
        ("fee_recipient", ByteVector(20)),
        ("state_root", ByteVector(32)),
        ("receipts_root", ByteVector(32)),
        ("logs_bloom", ByteVector(256)),
        ("prev_randao", ByteVector(32)),
        ("block_number", uint64),
        ("gas_limit", uint64),
        ("gas_used", uint64),
        ("timestamp", uint64),
        ("extra_data", ByteList(32)),
        ("base_fee_per_gas", ByteVector(32)),
        ("block_hash", ByteVector(32)),
        ("transactions_root", ByteVector(32)),
        ("withdrawals_root", ByteVector(32)),
        ("blob_gas_used", uint64),
        ("excess_blob_gas", uint64),
        ("parent_beacon_block_root", ByteVector(32)),
    )


PubKeyVector = Vector(ByteVector(48), 512)
Branches = List(ByteVector(32), 32)


class SyncCommittee(Serializable):
    fields = (
        ("pubkeys", PubKeyVector),
        ("aggregate_pubkey", ByteVector(48)),
    )


class SyncAggregate(Serializable):
    fields = (
        ("sync_committee_bits", ByteVector(64)),
        ("sync_committee_signature", ByteVector(96)),
    )


class LightClientBootstrap(Serializable):
    fields = (
        ("beacon_header", BeaconBlockHeader),
        ("current_sync_committee", SyncCommittee),
        ("current_sync_committee_branch", Branches),
        ("execution_header", ExecutionPayloadHeader),
        ("execution_branch", Branches),
    )


class LightClientUpdate(Serializable):
    fields = (
        ("attested_header", BeaconBlockHeader),
        ("next_sync_committee", SyncCommittee),
        ("next_sync_committee_branch", Branches),
        ("finalized_header", BeaconBlockHeader),
        ("finality_branch", Branches),
        ("sync_aggregate", SyncAggregate),
        ("signature_slot", uint64),
        ("execution_header", ExecutionPayloadHeader),
        ("execution_branch", Branches),
    )


beacon = BeaconBlockHeader(
    slot=1234,
    proposer_index=42,
    parent_root=bytes_seq(32, 0x10),
    state_root=bytes_seq(32, 0x20),
    body_root=bytes_seq(32, 0x30),
)

def byte_list_sequence(length: int, seed: int):
    return tuple(bytes([(seed + i) % 256]) for i in range(length))


execution = ExecutionPayloadHeader(
    parent_hash=bytes_seq(32, 0x01),
    fee_recipient=bytes_seq(20, 0x02),
    state_root=bytes_seq(32, 0x03),
    receipts_root=bytes_seq(32, 0x04),
    logs_bloom=bytes_seq(256, 0x05),
    prev_randao=bytes_seq(32, 0x06),
    block_number=555,
    gas_limit=16_000_000,
    gas_used=15_000_000,
    timestamp=1_694_000_123,
    extra_data=byte_list_sequence(12, 0x07),
    base_fee_per_gas=bytes_seq(32, 0x08),
    block_hash=bytes_seq(32, 0x09),
    transactions_root=bytes_seq(32, 0x0A),
    withdrawals_root=bytes_seq(32, 0x0B),
    blob_gas_used=1024,
    excess_blob_gas=2048,
    parent_beacon_block_root=bytes_seq(32, 0x0C),
)

pubkeys = [bytes_seq(48, i % 255) for i in range(512)]
committee = SyncCommittee(
    pubkeys=pubkeys,
    aggregate_pubkey=bytes_seq(48, 0xAA),
)

aggregate = SyncAggregate(
    sync_committee_bits=bytes_seq(64, 0xCC),
    sync_committee_signature=bytes_seq(96, 0xDD),
)

branch5 = [bytes_seq(32, 0x50 + i) for i in range(5)]
branch4 = [bytes_seq(32, 0x60 + i) for i in range(4)]

bootstrap = LightClientBootstrap(
    beacon_header=beacon,
    current_sync_committee=committee,
    current_sync_committee_branch=branch5,
    execution_header=execution,
    execution_branch=branch4,
)

update = LightClientUpdate(
    attested_header=beacon,
    next_sync_committee=committee,
    next_sync_committee_branch=branch5,
    finalized_header=beacon,
    finality_branch=branch5,
    sync_aggregate=aggregate,
    signature_slot=987_654,
    execution_header=execution,
    execution_branch=branch4,
)


def hexify(data: bytes) -> str:
    return "0x" + data.hex()


def make_record(value: Serializable) -> Dict[str, str]:
    return {
        "ssz": hexify(ssz.encode(value)),
        "root": hexify(ssz.get_hash_tree_root(value)),
    }


vectors = {
    "BeaconBlockHeader": make_record(beacon),
    "ExecutionPayloadHeader": make_record(execution),
    "SyncCommittee": make_record(committee),
    "SyncAggregate": make_record(aggregate),
    "LightClientBootstrap": make_record(bootstrap),
    "LightClientUpdate": make_record(update),
}

out_file = (
    repo_root
    / "tests"
    / "LightClientVectors"
    / "ssz"
    / "py-ssz"
    / "light_client_vectors.json"
)
out_file.parent.mkdir(parents=True, exist_ok=True)
out_file.write_text(json.dumps(vectors, indent=2) + "\n")
print(f"Wrote {out_file}")
