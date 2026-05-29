[CmdletBinding()]
param (
  [Parameter(Mandatory)]
  [string]$AcrName,

  [Parameter(Mandatory)]
  [string]$ExtensionName,

  [Parameter(Mandatory)]
  [string]$Version
)

$ErrorActionPreference = 'Stop'

function Resolve-BinaryPath {
  param(
    [Parameter(Mandatory)]
    [string]$RelativeFolder,

    [Parameter(Mandatory)]
    [string]$FileName,

    [Parameter(Mandatory)]
    [string]$Description
  )

  $primaryPath = Join-Path (Join-Path $env:PIPELINE_WORKSPACE $RelativeFolder) $FileName
  if (Test-Path -Path $primaryPath -PathType Leaf) {
    return $primaryPath
  }

  throw "$Description binary not found at expected path: $primaryPath"
}

$linuxBin = Resolve-BinaryPath -RelativeFolder 'extension-binaries/linux-x64' -FileName 'gen-kv-secret-extension' -Description 'Linux'
$linuxArmBin = Resolve-BinaryPath -RelativeFolder 'extension-binaries/linux-arm64' -FileName 'gen-kv-secret-extension' -Description 'Linux Arm'
$winBin = Resolve-BinaryPath -RelativeFolder 'extension-binaries/win-x64' -FileName 'gen-kv-secret-extension.exe' -Description 'Windows'
$osxBin = Resolve-BinaryPath -RelativeFolder 'extension-binaries/osx-arm64' -FileName 'gen-kv-secret-extension' -Description 'macOS'

if ($IsLinux -or $IsMacOS) {
  # Pipeline artifact download can strip executable bits from binaries.
  & chmod +x $linuxBin
  & chmod +x $linuxArmBin
  & chmod +x $osxBin
}

$target = ("br:{0}.azurecr.io/extensions/{1}:{2}" -f $AcrName, $ExtensionName, $Version)

Write-Host "Publishing extension package to $target"
~/.azure/bin/bicep publish-extension --target $target --bin-linux-x64 $linuxBin --bin-linux-arm64 $linuxArmBin --bin-win-x64 $winBin --bin-osx-arm64 $osxBin --force
