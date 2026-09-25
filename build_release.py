"""Build the Windows GUI and self-extracting installer from the published sources."""

import os
import re
import subprocess
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZIP_STORED, ZipFile


ROOT = Path(__file__).resolve().parent
PAYLOAD = ROOT / "payload"
DIST = ROOT / "dist"
GUI_NAME = "Anima_INT8_ConvRot_Converter_GUI.exe"
INSTALLER_NAME = "anima_int8_convrot_converter.exe"
FILE_NAMES = (
    "Anima_INT8_ConvRot_Converter_GUI.cmd",
    "model_inspect.py",
    "verify_runtime.py",
    "setup_portable.ps1",
    "hardware_profile.ps1",
    "README_portable_ko.txt",
    "THIRD_PARTY_NOTICES.txt",
    "converter/LICENSE",
    "converter/quant_int8_convrot.py",
    "release_version.txt",
)


def main() -> None:
    if os.name != "nt":
        raise SystemExit("The native Windows EXE build requires Windows.")
    version = (PAYLOAD / "release_version.txt").read_text(encoding="utf-8").strip()
    gui_source = (ROOT / "src/native_gui/Program.cs").read_text(encoding="utf-8")
    gui_version = re.search(r'private const string AppVersion = "([^"]+)";', gui_source)
    if not gui_version or gui_version.group(1) != version:
        raise SystemExit("Distribution and GUI information versions differ.")

    bundle = Path(os.environ.get("PYTHON310_BUNDLE", str(ROOT / "vendor/python310.zip")))
    if not bundle.is_file():
        raise SystemExit(f"Set PYTHON310_BUNDLE to the verified Python 3.10.11 runtime ZIP: {bundle}")
    csc = Path(os.environ.get("WINDIR", r"C:\Windows")) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
    if not csc.is_file():
        raise SystemExit(f".NET Framework C# compiler was not found: {csc}")
    DIST.mkdir(exist_ok=True)

    gui_exe = DIST / GUI_NAME
    subprocess.run(
        [
            str(csc), "/nologo", "/target:winexe", f"/out:{gui_exe}",
            "/reference:System.Windows.Forms.dll",
            "/reference:System.Drawing.dll",
            "/reference:System.Web.Extensions.dll",
            str(ROOT / "src/native_gui/Program.cs"),
        ],
        check=True,
    )

    archive_path = DIST / "payload.bin"
    with ZipFile(archive_path, "w", compression=ZIP_DEFLATED, compresslevel=9) as archive:
        archive.write(gui_exe, GUI_NAME)
        for name in FILE_NAMES:
            archive.write(PAYLOAD / name, name)
        archive.write(bundle, "python310.zip", compress_type=ZIP_STORED)

    installer_exe = DIST / INSTALLER_NAME
    subprocess.run(
        [
            str(csc), "/nologo", "/target:winexe", f"/out:{installer_exe}",
            f"/resource:{archive_path},payload.zip",
            "/reference:System.Windows.Forms.dll",
            "/reference:System.Drawing.dll",
            "/reference:System.IO.Compression.dll",
            "/reference:System.IO.Compression.FileSystem.dll",
            str(ROOT / "src/installer/Program.cs"),
        ],
        check=True,
    )
    print(f"Built {installer_exe} ({installer_exe.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
