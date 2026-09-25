"""Check a safetensors source header before running the upstream converter."""

import argparse
import json
import struct
from pathlib import Path


def inspect(path: Path) -> dict:
    with path.open("rb") as source:
        prefix = source.read(8)
        if len(prefix) != 8:
            raise ValueError("safetensors header is missing")
        header_size = struct.unpack("<Q", prefix)[0]
        if header_size < 2 or header_size > 100_000_000:
            raise ValueError("safetensors header size is invalid")
        header_bytes = source.read(header_size)
        if len(header_bytes) != header_size:
            raise ValueError("safetensors header is incomplete")
    header = json.loads(header_bytes)
    if not isinstance(header, dict):
        raise ValueError("safetensors header is not an object")
    tensors = {key: value for key, value in header.items() if key != "__metadata__"}
    weights = {key: value for key, value in tensors.items() if key.endswith(".weight")}
    if not weights:
        raise ValueError("no model weights were found")
    if any(key.endswith(".comfy_quant") for key in tensors):
        raise ValueError("the source already contains Comfy quantization metadata")
    if any(value.get("dtype") in ("I8", "U8") for value in weights.values()):
        raise ValueError("the source already contains INT8 model weights")
    return {"source": str(path), "tensor_count": len(tensors), "weight_count": len(weights)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    print(json.dumps(inspect(args.source), ensure_ascii=False))


if __name__ == "__main__":
    main()
