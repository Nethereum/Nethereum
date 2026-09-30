#!/usr/bin/env python3
"""Prepare a Zisk input file from raw data chunks.

Zisk input format (memory-mapped at 0x40000000):
  [8 bytes: total_size (little-endian u64)]
  For each chunk:
    [8 bytes: chunk_len (little-endian u64)]
    [chunk_data, padded to 8-byte alignment]

Usage: python3 prepare-zisk-input.py <input_file> <output_file>
"""
import sys
import struct

def prepare_input(data_chunks):
    """Convert list of byte chunks into Zisk input format."""
    body = b""
    for chunk in data_chunks:
        chunk_len = len(chunk)
        padded_len = (chunk_len + 7) & ~7  # align to 8 bytes
        body += struct.pack("<Q", chunk_len)  # u64 little-endian length
        body += chunk
        body += b"\x00" * (padded_len - chunk_len)  # padding

    total_size = len(body)
    return struct.pack("<Q", total_size) + body

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(f"Usage: {sys.argv[0]} <input_file> <output_file>")
        sys.exit(1)

    with open(sys.argv[1], "rb") as f:
        raw_data = f.read()

    result = prepare_input([raw_data])

    with open(sys.argv[2], "wb") as f:
        f.write(result)

    print(f"Input: {len(raw_data)} bytes -> Zisk format: {len(result)} bytes")
