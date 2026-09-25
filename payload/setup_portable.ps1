$ErrorActionPreference = 'Stop'
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$runtime = Join-Path $appDir 'runtime'
$baseDir = Join-Path $runtime 'python310'
$basePython = Join-Path $baseDir 'python.exe'
$venv = Join-Path $runtime '.venv'
$python = Join-Path $venv 'Scripts\python.exe'
$ready = Join-Path $runtime 'READY'
$bundle = Join-Path $appDir 'python310.zip'
$cudaRecord = Join-Path $runtime 'cuda_build.txt'
$hardwareScript = Join-Path $appDir 'hardware_profile.ps1'
$verifyScript = Join-Path $appDir 'verify_runtime.py'

if (-not (Test-Path -LiteralPath $hardwareScript)) { throw 'Hardware profile script is missing.' }
if (-not (Test-Path -LiteralPath $verifyScript)) { throw 'Runtime verification script is missing.' }
. $hardwareScript
$gpu = Get-GpuProfile
Write-Host "GPU: $($gpu.Name); driver $($gpu.Driver); compute capability $($gpu.Capability); VRAM $($gpu.MemoryMiB) MiB"
Write-Host "Selected PyTorch CUDA build: $($gpu.CudaBuild)"
if ($gpu.MemoryMiB -lt 8192) {
    Write-Warning 'Less than 8 GiB VRAM is available. Large model conversion may run out of GPU memory.'
}

New-Item -ItemType Directory -Force -Path $runtime | Out-Null
if (Test-Path -LiteralPath $ready) { Remove-Item -LiteralPath $ready -Force }
if (-not (Test-Path -LiteralPath $basePython)) {
    if (-not (Test-Path -LiteralPath $bundle)) { throw 'Bundled Python 3.10 archive is missing.' }
    Write-Host 'Extracting bundled Python 3.10.11 into the application folder...'
    New-Item -ItemType Directory -Force -Path $baseDir | Out-Null
    Expand-Archive -LiteralPath $bundle -DestinationPath $baseDir -Force
    if (-not (Test-Path -LiteralPath $basePython)) { throw 'Bundled Python extraction failed.' }
}
& $basePython -c 'import sys,venv,ensurepip; assert sys.version_info[:2] == (3,10); print(sys.version)'
if ($LASTEXITCODE -ne 0) { throw 'Local Python 3.10 verification failed.' }

if (Test-Path -LiteralPath $python) {
    & $python -c 'import sys; assert sys.version_info[:2] == (3,10)'
    if ($LASTEXITCODE -ne 0) { throw 'Existing venv uses another Python version. Extract into a fresh folder.' }
}

if (-not (Test-Path -LiteralPath $python)) {
    Write-Host 'Creating the local Python virtual environment...'
    & $basePython -m venv $venv
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create the local virtual environment.' }
}

# A prior attempt may have installed every package before its final verification
# failed. Verify that local venv first so a repair does not repeat large downloads.
if ((Test-Path -LiteralPath (Join-Path $venv 'Lib\site-packages\torch')) -and
    (Test-Path -LiteralPath (Join-Path $venv 'Lib\site-packages\comfy_kitchen'))) {
    Write-Host 'Checking the existing app-local environment before downloading packages...'
    & $python -B $verifyScript $gpu.CudaBuild
    if ($LASTEXITCODE -eq 0) {
        Set-Content -LiteralPath $cudaRecord -Value $gpu.CudaBuild -Encoding Ascii
        Set-Content -LiteralPath $ready -Value 'ready' -Encoding Ascii
        Write-Host 'Installation complete; existing packages are ready.'
        return
    }
    Write-Host 'Existing packages did not pass verification; continuing installation.'
}

Write-Host 'Installing the selected CUDA PyTorch build. The download may be several GB...'
& $python -m pip install "torch==2.7.1+$($gpu.CudaBuild)" --index-url "https://download.pytorch.org/whl/$($gpu.CudaBuild)" --extra-index-url 'https://pypi.org/simple'
if ($LASTEXITCODE -ne 0) { throw 'PyTorch installation failed.' }
& $python -m pip install 'numpy==1.26.4' 'safetensors==0.8.0' 'packaging>=24,<27' --index-url 'https://pypi.org/simple'
if ($LASTEXITCODE -ne 0) { throw 'Converter dependency installation failed.' }

# The converter uses comfy-kitchen's pure PyTorch rotation helpers. Its native
# 0.2.31 wheel requires an R580+ driver; the official pure Python wheel avoids
# that unrelated CUDA 13 requirement on older supported NVIDIA drivers.
$wheelDir = Join-Path $runtime 'wheels'
New-Item -ItemType Directory -Force -Path $wheelDir | Out-Null
Write-Host 'Downloading the official pure Python comfy-kitchen wheel...'
& $python -m pip download 'comfy-kitchen==0.2.31' --no-deps --only-binary=:all: --platform any --python-version 310 --index-url 'https://pypi.org/simple' --dest $wheelDir
if ($LASTEXITCODE -ne 0) { throw 'comfy-kitchen download failed.' }
$kitchenWheel = Join-Path $wheelDir 'comfy_kitchen-0.2.31-py3-none-any.whl'
if (-not (Test-Path -LiteralPath $kitchenWheel)) { throw 'The pure Python comfy-kitchen wheel was not downloaded.' }
$expectedWheelHash = '5117946c30f308cfc73b9c26f723ae3918308bd090e57a8eae298406934aabd6'
if ((Get-FileHash -LiteralPath $kitchenWheel -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedWheelHash) {
    throw 'The comfy-kitchen wheel checksum does not match PyPI.'
}
& $python -m pip install --no-deps --force-reinstall $kitchenWheel
if ($LASTEXITCODE -ne 0) { throw 'comfy-kitchen installation failed.' }

& $python -B $verifyScript $gpu.CudaBuild
if ($LASTEXITCODE -ne 0) { throw 'CUDA or converter verification failed.' }

Set-Content -LiteralPath $cudaRecord -Value $gpu.CudaBuild -Encoding Ascii
Set-Content -LiteralPath $ready -Value 'ready' -Encoding Ascii
Write-Host 'Installation complete.'
