[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [long]$RunId,

    [pscredential]$Credential
)

$ErrorActionPreference = 'Stop'
$healthUrl = 'https://backpack.lanhamchristian.org/health/live'
$releaseName = "$RunId-$([guid]::NewGuid().ToString('N'))"
$download = Join-Path ([IO.Path]::GetTempPath()) "Backpack-$releaseName"
$remoteRoot = "C:\Deploy\BackpackReleases\$releaseName"
$session = $null
$previousPath = $null
$switched = $false

try {
    gh run download $RunId --repo LCSchorton/LCSLifecycle --name Backpack-IIS --dir $download
    if ($LASTEXITCODE -ne 0) { throw 'GitHub artifact download failed.' }
    $archive = Join-Path $download 'Backpack.zip'
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'Backpack.zip is missing.' }

    if (-not $Credential) {
        $Credential = Get-Credential -Message 'Enter your LCSSS admin credentials'
    }
    $session = New-PSSession -ComputerName lcsss -Credential $Credential
    $previousPath = Invoke-Command -Session $session {
        $ErrorActionPreference = 'Stop'
        Import-Module WebAdministration
        $site = Get-Item 'IIS:\Sites\Backpack'
        if ($site.applicationPool -ne 'Backpack') { throw 'The Backpack site must use the Backpack app pool.' }
        New-Item -ItemType Directory -Path $using:remoteRoot -Force | Out-Null
        $site.physicalPath
    }

    Copy-Item -LiteralPath $archive -Destination "$remoteRoot\Backpack.zip" -ToSession $session
    Invoke-Command -Session $session {
        $ErrorActionPreference = 'Stop'
        $root = $using:remoteRoot
        Expand-Archive -LiteralPath "$root\Backpack.zip" -DestinationPath "$root\site"
        if (-not (Test-Path "$root\site\web.config") -or
            -not (Test-Path "$root\site\Lcs.Web.dll")) {
            throw 'The artifact does not contain a complete Backpack publish.'
        }

        icacls.exe "$root\site" /grant 'IIS AppPool\Backpack:(OI)(CI)(RX)' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not grant the Backpack app pool read access.' }
    }

    $switched = $true
    Invoke-Command -Session $session {
        $ErrorActionPreference = 'Stop'
        Import-Module WebAdministration
        Set-ItemProperty 'IIS:\Sites\Backpack' -Name physicalPath -Value "$using:remoteRoot\site"
        Restart-WebAppPool -Name 'Backpack'
    }

    $healthy = $false
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 10
            if ($response.StatusCode -eq 200 -and $response.Content.Trim() -eq 'Healthy') {
                $healthy = $true
                break
            }
        }
        catch {
            Write-Verbose $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    if (-not $healthy) { throw "Backpack did not report Healthy at $healthUrl." }
    "Backpack run $RunId is healthy. Previous IIS path: $previousPath"
}
catch {
    if ($switched) {
        try {
            Invoke-Command -Session $session {
                $ErrorActionPreference = 'Stop'
                Import-Module WebAdministration
                Set-ItemProperty 'IIS:\Sites\Backpack' -Name physicalPath -Value $using:previousPath
                Restart-WebAppPool -Name 'Backpack'
            }
            Write-Warning "IIS path restored to $previousPath."
        }
        catch {
            Write-Warning "IIS rollback failed. Restore the Backpack site path to $previousPath. $($_.Exception.Message)"
        }
    }
    throw
}
finally {
    if ($session) { Remove-PSSession $session }
    if (Test-Path -LiteralPath $download) {
        Remove-Item -LiteralPath $download -Recurse -Force -ErrorAction SilentlyContinue
    }
}
