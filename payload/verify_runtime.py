"""Verify the application-local Python and CUDA conversion dependencies."""

import argparse
import sys


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("cuda_build", choices=("cu118", "cu126", "cu128"))
    args = parser.parse_args()

    import comfy_kitchen
    import numpy
    import safetensors
    import torch
    from comfy_kitchen.tensor.int8_utils import _build_hadamard, _rotate_weight

    if sys.version_info[:2] != (3, 10):
        raise RuntimeError(f"Expected Python 3.10, found {sys.version.split()[0]}")
    if not torch.__version__.startswith("2.7.1+" + args.cuda_build):
        raise RuntimeError(f"Expected PyTorch 2.7.1+{args.cuda_build}, found {torch.__version__}")
    if numpy.__version__ != "1.26.4":
        raise RuntimeError(f"Expected NumPy 1.26.4, found {numpy.__version__}")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA GPU unavailable")
    if torch.cuda.get_device_capability(0) < (7, 5):
        raise RuntimeError("GPU compute capability is below 7.5")

    weight = torch.ones((16, 16), device="cuda")
    hadamard = _build_hadamard(16, device="cuda")
    rotated = _rotate_weight(weight, hadamard, 16)
    if not rotated.is_cuda:
        raise RuntimeError("CUDA ConvRot verification failed")
    torch.cuda.synchronize()
    print("CUDA ready:", torch.__version__, torch.cuda.get_device_name(0))


if __name__ == "__main__":
    main()
