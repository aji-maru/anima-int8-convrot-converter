Anima INT8 ConvRot 사용법
=======================

설치·업데이트와 실행
--------------------

Windows 10/11 64비트와 NVIDIA GPU가 필요합니다. GPU 연산 능력(compute capability)은 7.5 이상이어야 합니다. Windows에 Python이 설치되어 있지 않아도 압축 해제 프로그램과 GUI를 실행할 수 있습니다.

anima_int8_convrot_converter.exe는 지정한 폴더에 배포 파일을 설치하거나 기존 설치를 업데이트합니다. 기본 경로는 EXE 옆의 anima_int8_convrot_converter 폴더입니다. 설치 후 GUI를 자동 실행하거나 Python을 설치하지 않습니다.

배포 EXE로 기존 설치를 업데이트하려면 실행 중인 GUI를 닫고, 최신 배포 EXE를 실행해 기존 anima_int8_convrot_converter 폴더를 지정하세요. EXE 화면에 설치하려는 버전이 표시됩니다. 설치 폴더의 release_version.txt에 기록된 버전이 같으면 파일을 바꾸지 않습니다. 버전이 다르면 높고 낮음에 관계없이 내장 파일과 설치된 파일의 SHA-256 값을 비교해 달라진 배포 파일만 교체합니다. 이전 설치에 버전 기록이 없으면 한 번 비교·업데이트하고 버전을 기록합니다. 확인 창에는 파일 목록 대신 추가·교체 파일 수를 표시합니다. 설치 후 생성된 runtime 폴더와 사용자 모델 파일은 건드리지 않으며, Python을 실행하지 않습니다. 교체 중 실패하면 이미 바꾼 파일을 원래 상태로 되돌리려고 시도합니다. 이전 버전의 배포 EXE에는 이 업데이트 기능이 없습니다.

압축 해제된 폴더에서 Anima_INT8_ConvRot_Converter_GUI.exe를 실행하세요. 이 GUI는 Windows 프로그램이며 Python을 사용하지 않습니다. 옵션 → 파이썬 및 의존성 설치를 선택하고 확인 창에서 예를 누르면 앱에 포함된 Python 3.10.11을 runtime/python310에 풀고, 그 Python으로 runtime/.venv를 만듭니다. 이후 모든 의존성 설치와 변환 코드는 venv의 Python을 사용합니다. Windows에 설치된 기본 Python은 사용하거나 변경하지 않습니다.

처음 설치할 때 인터넷 연결과 충분한 저장 공간이 필요합니다. PyTorch 다운로드는 수 GB일 수 있습니다. 설치 코드는 NVIDIA 드라이버 버전과 GPU 연산 능력을 읽고 PyTorch 2.7.1의 CUDA 11.8, 12.6, 12.8 빌드 중 하나를 선택합니다. Blackwell GPU에는 CUDA 12.8 빌드와 Windows 드라이버 570.65 이상이 필요합니다. 다른 지원 GPU에는 드라이버 570.65 이상이면 CUDA 12.8, 560.76 이상이면 CUDA 12.6, 520.06 이상이면 CUDA 11.8을 선택합니다. 조건을 충족하지 않으면 설치를 중지하고 이유를 로그에 표시합니다. 시스템 CUDA Toolkit은 설치하거나 변경하지 않습니다.

comfy-kitchen은 변환 코드가 사용하는 순수 PyTorch 회전 함수를 포함한 공식 순수 Python 휠을 설치합니다. CUDA 13 네이티브 휠의 드라이버 580 이상 조건을 이 변환 작업에 강제하지 않기 위한 선택입니다. 설치 후 선택한 PyTorch 빌드와 실제 GPU CUDA 연산을 확인합니다. GPU 메모리가 8GiB 미만이면 변환 중 메모리 부족 가능성을 경고합니다. 모델 크기별 최소 VRAM을 보장하지는 않습니다.

이전 버전에서 설치 마지막 단계만 실패했다면, 이 버전의 setup_portable.ps1, verify_runtime.py, Anima_INT8_ConvRot_Converter_GUI.exe를 기존 앱 폴더에 복사한 뒤 GUI의 설치 메뉴를 다시 실행하세요. 기존 venv가 CUDA 검증을 통과하면 PyTorch와 다른 패키지를 다시 내려받지 않고 설치를 완료합니다.

Python 3.10.11은 이 도구에서 직접 검증한 버전이며 PyTorch 2.7.1과 comfy-kitchen 0.2.31의 Windows 휠을 사용할 수 있어 현재 번들로 유지합니다. Python 3.10의 공식 지원 종료 예정일은 2026년 10월이므로 이후에는 지원되는 Python 버전으로 번들을 갱신해야 합니다.

변환
----

1. 변환할 .safetensors 파일을 입력하거나 찾아보기로 선택하세요.
2. 출력 위치를 선택하세요. 원본 모델과 같은 위치는 변환 대상 파일의 폴더를 뜻합니다. 직접 경로 지정은 기존 폴더를 입력하거나 폴더 버튼으로 선택합니다.
3. 검증을 누르세요. GUI가 Comfy-Org 변환 코드의 --dry-run을 실행해 대상 층 수와 임베딩 수를 확인합니다.
4. 검증이 끝나면 실행을 누르세요. 실행은 GUI가 임시로 보관한 검증 결과를 사용하며 --dry-run을 다시 실행하지 않습니다.

출력 이름은 원본파일명_int8_convrot.safetensors입니다. 기존 파일은 덮어쓰지 않습니다. 진행 바와 시간 추정, 로그가 메인 화면에 표시됩니다. 작업 종료를 누르면 진행 중인 변환을 중지하고 임시 파일을 지웁니다.

Anima 1.0과 2.9B 외의 safetensors 모델도 선택할 수 있습니다. GUI는 이미 INT8로 양자화된 파일을 거르고, Comfy-Org 스크립트의 --dry-run 결과에서 변환할 층과 임베딩 수를 확인합니다. ConvRot 대상 층이 없으면 중지합니다. 검증 결과는 GUI를 닫을 때 사라집니다. 파일 경로를 바꾸거나 검증 후 원본 파일의 크기·수정 시각이 바뀌면 다시 검증해야 합니다. 별도의 base 모델 파일은 필요하지 않습니다. 새 구조의 변환 결과는 실제 이미지 생성으로 품질을 확인하세요.

폴더 구성
---------

- Anima_INT8_ConvRot_Converter_GUI.exe: Python 없이 실행되는 GUI
- Anima_INT8_ConvRot_Converter_GUI.cmd: GUI 실행 스크립트
- setup_portable.ps1: 앱 폴더의 Python 3.10 및 venv 의존성 설치
- hardware_profile.ps1: NVIDIA GPU와 드라이버 검사, CUDA 빌드 선택
- python310.zip: Python 3.10.11 런타임과 라이선스
- model_inspect.py: safetensors 입력 검사
- verify_runtime.py: 앱 폴더의 Python, 의존성 및 CUDA 연산 검사
- release_version.txt: 설치된 배포 버전 기록
- converter/: 변환 코드와 라이선스
- THIRD_PARTY_NOTICES.txt: 제3자 코드의 출처, 라이선스 및 변경 내역
- runtime/: GUI에서 설치한 Python과 venv (설치 후 생성)

가상환경은 공식 Python 문서에서 폴더 이동을 지원하지 않는다고 안내합니다. 설치가 끝난 폴더를 다른 위치나 PC로 옮긴 경우에는 설치 EXE를 새 위치에서 다시 실행하고 옵션 → 파이썬 및 의존성 설치를 진행하세요.

버전별 패치 내역
----------------

0.1 Beta
- 앱 폴더의 Python 3.10 가상환경 설치와 GPU에 맞는 CUDA PyTorch 선택 기능을 추가했습니다.
- safetensors 파일 검증, INT8 ConvRot 변환, 진행률과 남은 시간 표시를 추가했습니다.
- 검증 결과를 실행에 재사용하고, 도움말의 사용법·정보 창을 추가했습니다.
- 설치 마지막 단계의 Python 인용부호 오류를 수정하고 기존 venv 재검증 기능을 추가했습니다.
