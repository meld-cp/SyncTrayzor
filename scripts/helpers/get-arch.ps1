function Get-Arch {
    $archStr = (Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" -Name "PROCESSOR_ARCHITECTURE").PROCESSOR_ARCHITECTURE
    if ($archStr -like "*ARM64*") {
        return "arm64"
    } elseif ($archStr -like "*AMD64*") {
        return "amd64"
    } else {
        throw "Unsupported architecture: $archStr"
    }
}