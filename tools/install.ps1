param(
    [switch]$BackupExisting,
    [switch]$InitializeAgentDb
)

$ErrorActionPreference = 'Stop'

$BundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SkillsSource = Join-Path $BundleRoot 'm-skills'
$ToolsSource = Join-Path $BundleRoot 'fsx-tools'
$DestSkills = Join-Path $HOME '.copilot\m-skills'
$DestTools = Join-Path $DestSkills 'fsx-builder\tools'

if (!(Test-Path $SkillsSource)) { throw "Missing skills source: $SkillsSource" }
if (!(Test-Path $ToolsSource)) { throw "Missing FSX tools source: $ToolsSource" }

if ($BackupExisting -and (Test-Path $DestSkills)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backup = Join-Path (Split-Path -Parent $DestSkills) "m-skills-backup-$stamp"
    Copy-Item -Path $DestSkills -Destination $backup -Recurse -Force
    Write-Host "Backed up existing skills to $backup"
}

New-Item -ItemType Directory -Path $DestSkills -Force | Out-Null
Copy-Item -Path (Join-Path $SkillsSource '*') -Destination $DestSkills -Recurse -Force

New-Item -ItemType Directory -Path $DestTools -Force | Out-Null
Copy-Item -Path (Join-Path $ToolsSource '*') -Destination $DestTools -Recurse -Force

if ($InitializeAgentDb) {
    $agentDbCli = Join-Path $DestTools 'agent-db\agent_db.py'
    if (Test-Path $agentDbCli) {
        $python = Get-Command python -ErrorAction SilentlyContinue
        if ($python) {
            & python $agentDbCli init
            $accountsCsv = Join-Path $DestTools 'accounts.csv'
            if (Test-Path $accountsCsv) {
                & python $agentDbCli seed-accounts
            } else {
                Write-Warning "Initialized agent.db, but skipped account seeding because accounts.csv was not found at $accountsCsv"
            }
        } else {
            Write-Warning 'Python was not found; skipped agent.db initialization.'
        }
    } else {
        Write-Warning "agent_db.py not found at $agentDbCli; skipped agent.db initialization."
    }
}

Write-Host "Installed Clawpilot workflows to $DestSkills"
Write-Host "Installed FSX tools to $DestTools"
Write-Host 'Restart Clawpilot or reload skills before using the migrated workflows.'
