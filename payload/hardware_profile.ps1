function Select-CudaBuild {
    param(
        [Parameter(Mandatory = $true)][version]$Driver,
        [Parameter(Mandatory = $true)][version]$Capability
    )

    if ($Capability -lt [version]'7.5') {
        throw "GPU compute capability $Capability is below the supported minimum 7.5."
    }
    if ($Capability -ge [version]'10.0') {
        if ($Driver -lt [version]'570.65') {
            throw "Blackwell GPU requires driver 570.65 or newer for the CUDA 12.8 build (found $Driver)."
        }
        return 'cu128'
    }
    if ($Driver -ge [version]'570.65') { return 'cu128' }
    if ($Driver -ge [version]'560.76') { return 'cu126' }
    if ($Driver -ge [version]'520.06') { return 'cu118' }
    throw "NVIDIA driver $Driver is too old. Update to 520.06 or newer."
}

function Get-GpuProfile {
    $smi = Get-Command 'nvidia-smi.exe' -ErrorAction SilentlyContinue
    if (-not $smi) { throw 'NVIDIA driver or nvidia-smi.exe was not found.' }
    $lines = @(& $smi.Source --query-gpu=name,driver_version,compute_cap,memory.total --format=csv,noheader,nounits 2>&1)
    if ($LASTEXITCODE -ne 0 -or $lines.Count -lt 1) {
        throw 'Unable to read NVIDIA GPU details with nvidia-smi.'
    }
    $parts = @($lines[0].ToString() -split ',\s*')
    if ($parts.Count -ne 4) { throw "Unexpected nvidia-smi result: $($lines[0])" }
    try {
        $driver = [version]$parts[1]
        $capability = [version]$parts[2]
        $memoryMiB = [int]$parts[3]
    } catch {
        throw "Unable to parse nvidia-smi result: $($lines[0])"
    }
    $cudaBuild = Select-CudaBuild -Driver $driver -Capability $capability
    return [pscustomobject]@{
        Name = $parts[0]
        Driver = $driver.ToString()
        Capability = $capability.ToString()
        MemoryMiB = $memoryMiB
        CudaBuild = $cudaBuild
    }
}
