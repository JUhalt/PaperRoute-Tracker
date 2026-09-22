#requires -Version 7.0
<#
.SYNOPSIS
Plans, or explicitly creates, a disposable VirtualBox linked clone for v0.5 certification.
.DESCRIPTION
The default invocation performs read-only preflight and prints the proposed paths.
Only -Execute creates/registers a clone. This script never starts a VM, installs an
application, changes the original VM's persistent settings or snapshot contents,
or deletes any VM or directory. VirtualBox registers the new linked disk in the
original media registry and may discard its transient guest-status properties.
The clone depends on the original Clean snapshot: retain that snapshot and its disk.
The candidate/fixture share is read-only. A separate new output share is writable.
Clipboard, drag-and-drop, remote display and network are disabled unless -EnableNetwork
explicitly selects NAT. Guest Additions/login availability must be checked after boot.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [guid] $SourceVmId,

    [Parameter(Mandatory)]
    [guid] $CleanSnapshotId,

    [ValidateNotNullOrEmpty()]
    [string] $CleanSnapshotName = '00 - Clean Windows - No PaperRoute',

    [Parameter(Mandatory)]
    [ValidatePattern('^PaperRoute-v05-Cert-[A-Za-z0-9][A-Za-z0-9._-]{0,59}$')]
    [string] $VmName,

    [Parameter(Mandatory)]
    [string] $InputDirectory,

    [string] $VBoxManagePath = 'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe',
    [switch] $EnableNetwork,
    [switch] $Execute
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($SourceVmId -eq [guid]::Empty -or $CleanSnapshotId -eq [guid]::Empty) {
    throw 'The exact source VM and Clean snapshot UUIDs are required.'
}
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [IO.Path]::Combine($repositoryRoot, 'artifacts')
$vmBaseDirectory = [IO.Path]::Combine($repositoryRoot, 'PaperRoute.Tests', 'TestResults', 'VirtualMachines')
$vmDirectory = [IO.Path]::Combine($vmBaseDirectory, $VmName)
$outputDirectory = [IO.Path]::Combine($vmDirectory, 'CertificationOutput')
$minimumFreeBytes = 20GB # Conservative working headroom; linked disks can grow further.

function Assert-StrictDescendant([string] $Path, [string] $Parent) {
    $fullPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    $fullParent = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Parent))
    if (-not $fullPath.StartsWith($fullParent + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "The resolved path '$fullPath' must be strictly beneath '$fullParent'."
    }
    return $fullPath
}

function Assert-NoReparseAncestors([string] $Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrEmpty($candidate)) {
        if (Test-Path -LiteralPath $candidate) {
            $item = Get-Item -LiteralPath $candidate -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "A certification path cannot traverse a reparse point: '$candidate'."
            }
        }
        $parent = [IO.Path]::GetDirectoryName($candidate)
        if ($parent -eq $candidate) { break }
        $candidate = $parent
    }
}

function Invoke-VBox([string[]] $Arguments) {
    $result = @(& $VBoxManagePath @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "VBoxManage failed ($LASTEXITCODE): $($Arguments -join ' ')`n$($result -join [Environment]::NewLine)"
    }
    return ($result -join [Environment]::NewLine)
}

function Read-XmlFile([string] $Path) {
    $document = [xml]::new()
    $document.XmlResolver = $null
    $document.Load($Path)
    return $document
}

function Assert-OriginalPersistentConfiguration([xml] $Before, [xml] $After, [string] $CloneDirectory) {
    $normalizedBefore = [xml]$Before.CloneNode($true)
    $normalizedAfter = [xml]$After.CloneNode($true)
    $oldDiskIds = @($normalizedBefore.SelectNodes('//*[local-name()="HardDisk"]') |
        ForEach-Object { $_.GetAttribute('uuid') })
    $newDisks = @($normalizedAfter.SelectNodes('//*[local-name()="HardDisk"]') |
        Where-Object { $_.GetAttribute('uuid') -notin $oldDiskIds })
    if ($newDisks.Count -ne 1) {
        throw 'Expected exactly one new linked disk registration beneath the original snapshot.'
    }
    foreach ($diskNode in $newDisks) {
        $null = Assert-StrictDescendant -Path $diskNode.GetAttribute('location') -Parent $CloneDirectory
        if ($diskNode.ParentNode.GetAttribute('uuid') -notin $oldDiskIds) {
            throw 'The new linked disk has an unexpected parent.'
        }
        $null = $diskNode.ParentNode.RemoveChild($diskNode)
    }
    foreach ($document in @($normalizedBefore, $normalizedAfter)) {
        # VirtualBox drops transient focus/login status when serializing a VM.
        # Preserve and compare every persistent setting and existing media entry.
        foreach ($node in @($document.SelectNodes('//*[local-name()="GuestProperty" and contains(@flags,"TRANSIENT")]'))) {
            $null = $node.ParentNode.RemoveChild($node)
        }
    }
    if ($normalizedBefore.OuterXml -cne $normalizedAfter.OuterXml) {
        throw 'Original persistent VM configuration changed beyond linked-media registration and transient guest status.'
    }
}

if (-not (Test-Path -LiteralPath $VBoxManagePath -PathType Leaf)) {
    throw "VBoxManage was not found at '$VBoxManagePath'."
}
if (-not [IO.Path]::IsPathFullyQualified($InputDirectory)) {
    throw 'Specify the reviewed input folder using its absolute path.'
}
$inputPath = Assert-StrictDescendant -Path $InputDirectory -Parent $artifactRoot
if (-not (Test-Path -LiteralPath $inputPath -PathType Container)) {
    throw 'Prepare the candidate/fixture input folder before planning or creating the VM.'
}
$null = Assert-StrictDescendant -Path $vmDirectory -Parent $vmBaseDirectory
Assert-NoReparseAncestors -Path $inputPath
Assert-NoReparseAncestors -Path $vmDirectory
if (@(Get-ChildItem -LiteralPath $inputPath -Recurse -Force |
        Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) {
    throw 'The read-only input folder must contain ordinary files/directories, without reparse points.'
}
if (Test-Path -LiteralPath $vmDirectory) {
    throw "The target already exists. Choose a new certification name: '$vmDirectory'."
}

$virtualBoxHome = if ([string]::IsNullOrWhiteSpace($env:VBOX_USER_HOME)) {
    [IO.Path]::Combine([Environment]::GetFolderPath('UserProfile'), '.VirtualBox')
} else {
    [IO.Path]::GetFullPath($env:VBOX_USER_HOME)
}
$registryPath = [IO.Path]::Combine($virtualBoxHome, 'VirtualBox.xml')
if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) {
    throw 'An existing VirtualBox registry is required. This script does not initialize a new host profile.'
}
$registry = Read-XmlFile -Path $registryPath
if (@($registry.SelectNodes('//*[local-name()="SharedFolder"]')).Count -ne 0) {
    throw 'Global VirtualBox shared folders exist. Review host exposure before creating the certification clone.'
}
$sourceEntry = $registry.SelectSingleNode("//*[local-name()='MachineEntry' and @uuid='{$sourceVmId}']")
if ($null -eq $sourceEntry) { throw "The reviewed source VM '$sourceVmId' is not registered." }
$sourceConfigPath = [IO.Path]::GetFullPath($sourceEntry.GetAttribute('src'), $virtualBoxHome)
$sourceConfig = Read-XmlFile -Path $sourceConfigPath
$sourceMachine = $sourceConfig.SelectSingleNode('/*[local-name()="VirtualBox"]/*[local-name()="Machine"]')
$cleanSnapshot = $sourceConfig.SelectSingleNode("//*[local-name()='Snapshot' and @uuid='{$cleanSnapshotId}']")
if ($null -eq $cleanSnapshot -or $cleanSnapshot.GetAttribute('name') -ne $cleanSnapshotName) {
    throw 'The exact reviewed Clean snapshot could not be verified. No alternative snapshot is selected automatically.'
}
if ($cleanSnapshot.HasAttribute('stateFile')) {
    throw 'The reviewed Clean snapshot must be powered off, without a saved execution state.'
}
$hardware = $cleanSnapshot.SelectSingleNode('./*[local-name()="Hardware"]')
if (@($hardware.SelectNodes('.//*[local-name()="SharedFolder" or local-name()="HostDrive" or local-name()="DeviceFilter"]')).Count -ne 0) {
    throw 'The Clean snapshot has an unexpected host share/drive/device filter; review it before cloning.'
}
$sourceState = Invoke-VBox -Arguments @('showvminfo', $sourceVmId, '--machinereadable')
if ($sourceState -notmatch '(?m)^VMState="poweroff"\r?$') {
    throw 'The original VM must be powered off before the linked certification clone is created.'
}
$version = Invoke-VBox -Arguments @('--version')
if ($version -notmatch '^7\.2\.') {
    throw "This script was reviewed against VirtualBox 7.2; found '$version'. Review its CLI before proceeding."
}
$disk = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($vmDirectory))
if ($disk.AvailableFreeSpace -lt $minimumFreeBytes) {
    throw 'At least 20 GiB free host disk space is required as certification working headroom.'
}
$sourceHashBefore = (Get-FileHash -LiteralPath $sourceConfigPath -Algorithm SHA256).Hash
$sourceCurrentSnapshot = $sourceMachine.GetAttribute('currentSnapshot')
$plan = [ordered]@{
    Operation = if ($Execute) { 'Create/register only; do not start' } else { 'Read-only plan; add -Execute to create/register' }
    SourceVmId = $sourceVmId
    SourceConfigPath = $sourceConfigPath
    CleanSnapshotId = $cleanSnapshotId
    CleanSnapshotName = $cleanSnapshotName
    SourceConfigSha256 = $sourceHashBefore
    VmName = $VmName
    VmDirectory = $vmDirectory
    ReadOnlyInputShare = $inputPath
    WritableOutputShare = $outputDirectory
    Network = if ($EnableNetwork) { 'NAT; host loopback disabled' } else { 'Disabled' }
    Clipboard = 'Disabled'
    DragAndDrop = 'Disabled'
    GuestAdditionsAndLogin = 'Not verified for this Clean snapshot; inspect after boot without reading stored passwords'
    HostFreeGiB = [math]::Round($disk.AvailableFreeSpace / 1GB, 1)
    LinkedCloneDependency = 'Retain original Clean snapshot and base disk. No original snapshot is restored or deleted.'
}
if (-not $Execute) {
    $plan | ConvertTo-Json -Depth 4
    return
}

# No filesystem, registry, VM, or snapshot mutation occurs before this explicit boundary.
$newVmId = [guid]::NewGuid().ToString()
$null = New-Item -ItemType Directory -Path $vmBaseDirectory -Force
try {
    $null = Invoke-VBox -Arguments @('clonevm', $sourceVmId,
        '--snapshot', $cleanSnapshotId, '--options', 'Link', '--mode', 'machine',
        '--name', $VmName, '--uuid', $newVmId, '--basefolder', $vmBaseDirectory, '--register')
    $newConfigPath = [IO.Path]::Combine($vmDirectory, $VmName + '.vbox')
    if (-not (Test-Path -LiteralPath $newConfigPath -PathType Leaf)) {
        throw 'The clone was not created at the reviewed exact location; leave it powered off and inspect the registration.'
    }
    $arguments = @('modifyvm', $newVmId, '--clipboard-mode', 'disabled',
        '--clipboard-file-transfers', 'disabled', '--drag-and-drop', 'disabled',
        '--vrde', 'off', '--usb-card-reader', 'off', '--audio-in', 'off')
    for ($adapter = 1; $adapter -le 8; $adapter++) {
        $mode = if ($adapter -eq 1 -and $EnableNetwork) { 'nat' } else { 'none' }
        $arguments += @(('--nic' + $adapter), $mode)
    }
    if ($EnableNetwork) { $arguments += @('--nat-localhostreachable1', 'off') }
    $null = Invoke-VBox -Arguments $arguments

    # Remove inherited install/unattended media from the clone only. In particular,
    # do not expose the original unattended helper medium to this certification run.
    foreach ($controller in $hardware.SelectNodes('./*[local-name()="StorageControllers"]/*[local-name()="StorageController"]')) {
        foreach ($dvd in $controller.SelectNodes('./*[local-name()="AttachedDevice" and @type="DVD"]')) {
            $null = Invoke-VBox -Arguments @('storageattach', $newVmId,
                '--storagectl', $controller.GetAttribute('name'), '--port', $dvd.GetAttribute('port'),
                '--device', $dvd.GetAttribute('device'), '--type', 'dvddrive', '--medium', 'none')
        }
    }
    $null = New-Item -ItemType Directory -Path $outputDirectory
    $null = Invoke-VBox -Arguments @('sharedfolder', 'add', $newVmId,
        '--name', 'PaperRouteInputs', '--hostpath', $inputPath, '--readonly', '--automount')
    $null = Invoke-VBox -Arguments @('sharedfolder', 'modify', $newVmId,
        '--name', 'PaperRouteInputs', '--symlink-policy', 'forbidden')
    $null = Invoke-VBox -Arguments @('sharedfolder', 'add', $newVmId,
        '--name', 'PaperRouteEvidence', '--hostpath', $outputDirectory, '--automount')
    $null = Invoke-VBox -Arguments @('sharedfolder', 'modify', $newVmId,
        '--name', 'PaperRouteEvidence', '--symlink-policy', 'forbidden')

    $afterSource = Read-XmlFile -Path $sourceConfigPath
    $afterMachine = $afterSource.SelectSingleNode('/*[local-name()="VirtualBox"]/*[local-name()="Machine"]')
    Assert-OriginalPersistentConfiguration -Before $sourceConfig -After $afterSource -CloneDirectory $vmDirectory
    if ($afterMachine.GetAttribute('currentSnapshot') -ne $sourceCurrentSnapshot) {
        throw 'The original current snapshot changed during clone creation. The clone remains off; inspect before continuing.'
    }
    $cloneConfig = Read-XmlFile -Path $newConfigPath
    $shares = @($cloneConfig.SelectNodes('//*[local-name()="SharedFolder"]'))
    if ($shares.Count -ne 2) { throw 'The clone does not have exactly the two reviewed shares.' }
    $inputShare = @($shares | Where-Object { $_.GetAttribute('name') -eq 'PaperRouteInputs' })
    $outputShare = @($shares | Where-Object { $_.GetAttribute('name') -eq 'PaperRouteEvidence' })
    if ($inputShare.Count -ne 1 -or $outputShare.Count -ne 1 -or
            $inputShare[0].GetAttribute('hostPath') -ne $inputPath -or
            $inputShare[0].GetAttribute('writable') -eq 'true' -or
            $outputShare[0].GetAttribute('hostPath') -ne $outputDirectory -or
            $outputShare[0].GetAttribute('writable') -ne 'true') {
        throw 'The clone share configuration does not match the reviewed access boundaries.'
    }
    $newState = Invoke-VBox -Arguments @('showvminfo', $newVmId, '--machinereadable')
    if ($newState -notmatch '(?m)^VMState="poweroff"\r?$') { throw 'The new clone is unexpectedly running.' }
    $plan['VmId'] = $newVmId
    $plan['CreatedAtUtc'] = [DateTime]::UtcNow.ToString('O')
    $plan['OriginalPersistentConfigurationPreserved'] = $true
    $plan['SourceConfigSha256After'] = (Get-FileHash -LiteralPath $sourceConfigPath -Algorithm SHA256).Hash
    $plan['ExpectedSourceMetadataChanges'] = 'New linked disk registration; discarded transient guest-status properties only'
    $plan['StartCommandForLaterReview'] = "VBoxManage startvm $newVmId --type gui"
    $plan | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ([IO.Path]::Combine($vmDirectory, 'certification-vm.json')) -Encoding utf8
    $plan | ConvertTo-Json -Depth 4
} catch {
    Write-Warning "Creation/configuration did not complete. No VM was started. Preserve the partial clone '$newVmId' and '$vmDirectory' for inspection; this script does not unregister or delete it."
    throw
}
