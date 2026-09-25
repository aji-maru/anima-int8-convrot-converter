Anima INT8 ConvRot Converter — 0.1 Beta
======================================

Developed by aji-maru.

This Windows GUI runs Comfy-Org's quant_int8_convrot.py with a model picker,
an output location, a dry-run validation step, progress display, and a local
Python 3.10 environment installer. The distribution EXE extracts the app and
can update an existing installation when its recorded version differs.

End users
---------

1. Run anima_int8_convrot_converter.exe and choose a new or existing app folder.
2. Start Anima_INT8_ConvRot_Converter_GUI.exe from that folder.
3. On first use, select Options > Install Python and dependencies.
4. Choose a safetensors model and output location, click Validate, then Run.

The GUI is in Korean. Detailed Korean instructions are in
payload/README_portable_ko.txt. The end-user EXE does not need a system Python
installation. Installation and conversion use Python inside the app folder.
Model weights are not included.

Build from source
-----------------

Requirements: Windows x64, .NET Framework C# compiler (csc.exe), and Python
for the build script only. Obtain the verified Python 3.10.11 runtime archive
from the official Python distribution, including its LICENSE.txt. Put the
runtime ZIP at vendor/python310.zip or set PYTHON310_BUNDLE to its full path.

The Python runtime archive used for the initial 0.1 Beta build has SHA-256:
6b45b884d250bc57537878073361887e1dfa5c5adb6dfbee3dd1be91e9ea4d0b

Run: python build_release.py

The resulting installer is dist/anima_int8_convrot_converter.exe. Python is
used to build that EXE, but the EXE and GUI do not require system Python when
run by end users. The GUI installs an app-local Python and dependencies.

Licensing and attribution
-------------------------

The converter source is based on Comfy-Org's comfy-model-tools:
https://github.com/Comfy-Org/comfy-model-tools

It is licensed under GPL-3.0-only; see LICENSE.txt, payload/converter/LICENSE,
and payload/THIRD_PARTY_NOTICES.txt for source attribution and modifications.
The redistributed Python runtime retains its own license inside the runtime
archive. Dependencies are downloaded when the user chooses installation.

This repository contains the source for the GUI, installer, and conversion
scripts. It does not contain model weights, virtual environments, or an
installed Python runtime.
