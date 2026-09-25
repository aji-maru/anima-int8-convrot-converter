Anima INT8 ConvRot Converter — 0.1 Beta
======================================

개발: aji-maru

이 Windows 프로그램은 Comfy-Org의 quant_int8_convrot.py를 GUI에서 실행할 수
있게 합니다. 변환할 모델과 출력 위치를 선택하고, 사전 검증을 거쳐 변환을
실행할 수 있습니다. 진행률과 남은 시간도 표시합니다. 배포 EXE는 프로그램을
지정한 폴더에 풀며, 설치된 버전과 배포 버전이 다르면 변경된 파일만 갱신합니다.

사용 방법
---------

1. anima_int8_convrot_converter.exe를 실행하고 설치할 폴더를 선택합니다.
2. 설치된 폴더에서 Anima_INT8_ConvRot_Converter_GUI.exe를 실행합니다.
3. 처음 사용할 때는 옵션 > 파이썬 및 의존성 설치를 선택합니다.
4. 변환할 safetensors 모델과 출력 위치를 선택하고 검증을 누른 뒤 실행을 누릅니다.

GUI의 자세한 한국어 사용법은 payload/README_portable_ko.txt에 있습니다.
배포 EXE와 GUI를 실행하는 데 시스템에 설치된 Python은 필요하지 않습니다.
의존성 설치와 모델 변환에는 앱 폴더 안의 Python을 사용합니다. 모델 가중치는
포함되어 있지 않습니다.

소스에서 빌드
-------------

빌드에는 Windows x64, .NET Framework C# 컴파일러(csc.exe), 빌드 스크립트를
실행할 Python이 필요합니다. 공식 Python 배포본의 Python 3.10.11 런타임
압축 파일을 LICENSE.txt와 함께 준비하고 vendor/python310.zip에 놓거나
PYTHON310_BUNDLE 환경 변수에 해당 파일의 전체 경로를 지정합니다.

최초 0.1 Beta 빌드에 사용한 Python 런타임 압축 파일의 SHA-256:
6b45b884d250bc57537878073361887e1dfa5c5adb6dfbee3dd1be91e9ea4d0b

빌드 명령: python build_release.py

결과물은 dist/anima_int8_convrot_converter.exe입니다. 빌드 과정에만 별도의
Python이 필요하며, 완성된 EXE와 GUI의 실행에는 시스템 Python이 필요하지
않습니다. GUI가 앱 폴더 안에 Python과 의존성을 설치합니다.

라이선스와 출처
---------------

변환기 소스는 Comfy-Org의 comfy-model-tools를 기반으로 합니다:
https://github.com/Comfy-Org/comfy-model-tools

GPL-3.0-only 라이선스와 출처·변경 사항은 LICENSE.txt,
payload/converter/LICENSE, payload/THIRD_PARTY_NOTICES.txt를 확인하세요.
배포본에 포함된 Python 런타임의 라이선스는 해당 런타임 압축 파일 안에
보존되어 있습니다. 의존성은 사용자가 설치를 선택할 때 다운로드합니다.

이 저장소에는 GUI, 설치 프로그램, 변환 스크립트의 소스가 들어 있습니다.
모델 가중치, 가상환경, 설치된 Python 런타임은 저장소에 포함되지 않습니다.

English
-------

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
