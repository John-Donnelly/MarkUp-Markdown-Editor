param(
    [string]$ComputerName = "192.168.0.100",
    [int]$Port = 4723,
    [string]$RemoteLogPath = "$env:TEMP\appium-server.log"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Load-DotEnv {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
            continue
        }

        $separatorIndex = $line.IndexOf('=')
        if ($separatorIndex -le 0) {
            continue
        }

        $key = $line.Substring(0, $separatorIndex).Trim()
        $value = $line.Substring($separatorIndex + 1).Trim()

        if ($value.Length -ge 2) {
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($key) -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($key))) {
            [Environment]::SetEnvironmentVariable($key, $value)
        }
    }
}

$solutionRoot = Split-Path -Path $PSScriptRoot -Parent
Load-DotEnv -Path (Join-Path $solutionRoot '.env')

$username = [Environment]::GetEnvironmentVariable('UITEST_REMOTE_WINRM_USERNAME')
$password = [Environment]::GetEnvironmentVariable('UITEST_REMOTE_WINRM_PASSWORD')

if ([string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password)) {
    throw 'Set UITEST_REMOTE_WINRM_USERNAME and UITEST_REMOTE_WINRM_PASSWORD in the root .env file before running this script.'
}

$securePassword = ConvertTo-SecureString $password -AsPlainText -Force
$credential = [pscredential]::new($username, $securePassword)

$remoteResult = Invoke-Command -ComputerName $ComputerName -Credential $credential -Authentication Negotiate -ScriptBlock {
    param($Port, $RemoteLogPath)

    $ErrorActionPreference = 'Stop'
    $npmGlobalPath = Join-Path $env:APPDATA 'npm'
    if (-not ($env:Path -split ';' | Where-Object { $_ -eq $npmGlobalPath })) {
        $env:Path = "$env:Path;$npmGlobalPath"
    }

    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        throw 'Node.js is not installed on the remote machine.'
    }

    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        throw 'npm is not installed on the remote machine.'
    }

    if (-not (Get-Command appium -ErrorAction SilentlyContinue)) {
        cmd.exe /c "npm install -g appium 2>nul" | Out-Null
    }

    $installedDriversText = (& cmd.exe /c "npx appium driver list --installed 2>nul" | Out-String)
    if ($installedDriversText -notmatch '(?im)^\s*windows\b') {
        & cmd.exe /c "npx appium driver install windows 2>nul" | Out-Null
        $installedDriversText = (& cmd.exe /c "npx appium driver list --installed 2>nul" | Out-String)
    }

    $existingProcesses = @(Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and
        $_.CommandLine -match 'appium(\.cmd)?\s+server' -and
        $_.CommandLine -match ("--port\\s+{0}\\b" -f $Port)
    })

    $appiumCommand = 'npx appium server --address 0.0.0.0 --port ' + $Port + ' *> "' + $RemoteLogPath + '"'
    if ($existingProcesses.Count -eq 0) {
        Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $appiumCommand -WindowStyle Hidden | Out-Null
        Start-Sleep -Seconds 5
    }

    [pscustomobject]@{
        ComputerName = $env:COMPUTERNAME
        AppiumVersion = ((& cmd.exe /c "npx appium --version 2>nul") | Select-Object -First 1)
        WindowsDriverInstalled = [bool]($installedDriversText -match '(?im)^\s*windows\b')
        AppiumServerCommand = $appiumCommand
        AppiumLogPath = $RemoteLogPath
        ExistingServerProcessCount = $existingProcesses.Count
    }
} -ArgumentList $Port, $RemoteLogPath

$remoteResult | Format-List *
