<#
.SYNOPSIS
  Agent conformance harness (Level 2): does an AI coding agent DISCOVER the scaffolded BELLATRIX skills and
  subagents, and does it DELEGATE locator work (`find --snapshot` / the locator specialist) instead of reading a UI
  tree? No live app is needed: the probes use the recorded snapshot fixtures in tests/fixtures/snapshots/.

.DESCRIPTION
  Data-driven: agents come from scripts/conformance/agents.json, languages/domains from
  scripts/conformance/languages.json. Adding an agent is a data change.

  Headless CLI syntax - verified from official docs on 2026-10-27 and cross-checked against the installed CLI's
  --help where installed:
    claude   https://code.claude.com/docs/en/headless , https://code.claude.com/docs/en/cli-reference
    copilot  https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-programmatic-reference
    cursor   https://cursor.com/docs/cli/headless , https://cursor.com/docs/cli/reference/permissions
    devin    https://docs.devin.ai/cli , https://docs.devin.ai/cli/reference/permissions.md
    codex    https://learn.chatgpt.com/docs/non-interactive-mode , https://learn.chatgpt.com/docs/cli/reference
    gemini   https://geminicli.com/docs/cli/headless , https://geminicli.com/docs/reference/policy-engine
  (Exact per-agent arguments, notes and URLs: scripts/conformance/agents.json.)

  Per agent x language:
    1. temp repo + language marker + `git init`; `bellatrix init --agent <a> --language <l> --domains <supported>`
    2. DISCOVERY probe: the agent lists the bellatrix-* skills/subagents it sees; compared with the agent's manifest
    3. DELEGATION probe per domain (BELLATRIX_TRACE set): "find the locator for <target>, reply with the locator only".
       Pass = reply matches the answer key AND the trace shows `find --snapshot` (or the transcript shows the locator
       specialist subagent) AND no context-heavy call AND (where a transcript exists) no direct read of the snapshot.
    4. artifacts/conformance/<agent>-<language>.json  (aggregate: scripts/conformance-report.ps1 -> docs/contributing/conformance-report.md)

  COST: a real run invokes the agents' models and MAY CONSUME PAID / METERED USAGE. Use -DryRun first; a real run
  prints the number of agent calls and refuses to start without -Confirm. The harness never writes to your home
  directory and never starts or stops any driver.

.EXAMPLE
  powershell -File scripts/agent-conformance.ps1 -Agent all -Language all -DryRun
  powershell -File scripts/agent-conformance.ps1 -AgentTable scripts/conformance/selftest-agents.json -Language csharp -Confirm   # free self-test
  powershell -File scripts/agent-conformance.ps1 -Agent claude -Language csharp -Confirm
#>
param(
    [string]$Agent = "all",
    [string]$Language = "all",
    [ValidateSet("1", "all")][string]$Targets = "1",
    [switch]$DryRun,
    [switch]$Confirm,
    [string]$Exe = "bellatrix",
    [string]$AgentTable = "",
    [int]$TimeoutSeconds = 300,
    [string]$OutDir = ""
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repo "artifacts\conformance" }
if (-not $AgentTable) { $AgentTable = Join-Path $PSScriptRoot "conformance\agents.json" }
$agentRows = (Get-Content $AgentTable -Raw | ConvertFrom-Json).agents
$langTable = Get-Content (Join-Path $PSScriptRoot "conformance\languages.json") -Raw | ConvertFrom-Json
$fixtureDir = Join-Path $repo "tests\fixtures\snapshots"

function Select-Ids($requested, $all, $kind) {
    if ($requested -eq "all") { return @($all) }
    $ids = $requested -split "," | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ }
    foreach ($id in $ids) { if ($all -notcontains $id) { throw "Unknown $kind '$id'. One of: $($all -join ', '), all" } }
    return @($ids)
}
$agentIds = Select-Ids $Agent ($agentRows | ForEach-Object { $_.id }) "agent"
$langIds = Select-Ids $Language ($langTable.languages | ForEach-Object { $_.id }) "language"

# ---------- helpers ----------
function Quote-Arg([string]$a) {
    # Windows command-line quoting (CommandLineToArgvW rules).
    if ($a -eq "") { return '""' }
    if ($a -notmatch '[\s"]') { return $a }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    $bs = 0
    foreach ($c in $a.ToCharArray()) {
        if ($c -eq '\') { $bs++; continue }
        if ($c -eq '"') { [void]$sb.Append('\' * (2 * $bs + 1)); [void]$sb.Append('"'); $bs = 0; continue }
        if ($bs) { [void]$sb.Append('\' * $bs); $bs = 0 }
        [void]$sb.Append($c)
    }
    if ($bs) { [void]$sb.Append('\' * (2 * $bs)) }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Resolve-Binary($agent) {
    foreach ($name in $agent.binaries) {
        foreach ($cand in @("$name.exe", "$name.cmd", $name)) {
            $cmd = Get-Command $cand -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($cmd) { return $cmd.Source }
        }
    }
    return $null
}

function Invoke-Captured([string]$file, [string[]]$arguments, [string]$cwd, [hashtable]$envVars, [int]$timeoutSec) {
    # stdout/stderr go to temp FILES via cmd redirection (never a pipe that a child daemon could hold open);
    # bounded wait on the launcher only; the whole tree is killed on timeout.
    $out = [IO.Path]::GetTempFileName(); $err = [IO.Path]::GetTempFileName()
    $commandLine = (Quote-Arg $file) + ' ' + (($arguments | ForEach-Object { Quote-Arg $_ }) -join ' ')
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = '/d /s /c "' + $commandLine + ' > ' + (Quote-Arg $out) + ' 2> ' + (Quote-Arg $err) + ' < NUL"'
    $psi.WorkingDirectory = $cwd
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if ($envVars) { foreach ($k in $envVars.Keys) { $psi.EnvironmentVariables[$k] = [string]$envVars[$k] } }
    $p = [System.Diagnostics.Process]::Start($psi)
    $timedOut = -not $p.WaitForExit($timeoutSec * 1000)
    if ($timedOut) { try { & taskkill /PID $p.Id /T /F | Out-Null } catch { } }
    $result = [pscustomobject]@{
        exitCode = $(if ($timedOut) { -1 } else { $p.ExitCode })
        timedOut = $timedOut
        # [string] drops Get-Content's PSPath/PSProvider note properties (they make ConvertTo-Json explode).
        stdout = [string](Get-Content $out -Raw -ErrorAction SilentlyContinue)
        stderr = [string](Get-Content $err -Raw -ErrorAction SilentlyContinue)
    }
    Remove-Item $out, $err -ErrorAction SilentlyContinue
    return $result
}

function Test-Auth($agent, $bin, $cwd) {
    $auth = $agent.auth
    if (-not $auth) { return $true } # no documented auth-status check: a logged-out run shows up as ERROR instead
    if ($auth.args) {
        $r = Invoke-Captured $bin @($auth.args) $cwd @{} 60
        return ($r.exitCode -eq $auth.okExitCode)
    }
    foreach ($v in @($auth.anyEnv)) { if ($v -and [Environment]::GetEnvironmentVariable($v)) { return $true } }
    foreach ($f in @($auth.anyFile)) { if ($f -and (Test-Path ($f -replace '^~', $HOME))) { return $true } }
    return $false
}

function Expand-Args($template, $prompt, $repoDir) {
    return @($template | ForEach-Object { (($_ -replace '\{prompt\}', $prompt) -replace '\{repo\}', $repoDir) -replace '\{scripts\}', $PSScriptRoot })
}

function Get-Expected($repoDir, $agentId) {
    # Expected names = what the agent's manifest says was scaffolded for it.
    $m = Get-Content (Join-Path $repoDir ".bellatrix\manifest.$agentId.json") -Raw | ConvertFrom-Json
    $paths = @($m.files | ForEach-Object { $_.path })
    $skills = @($paths | Where-Object { $_ -match '/skills/([^/]+)/SKILL\.md$' } | ForEach-Object { $Matches[1] } | Sort-Object -Unique)
    $agents = @($paths | Where-Object { $_ -match '/agents/([^/]+?)(\.agent)?\.(md|toml)$' } | ForEach-Object { $Matches[1] } | Sort-Object -Unique)
    return [pscustomobject]@{ skills = $skills; agents = $agents }
}

function Get-ReplyText($agent, $stdout) {
    if (-not $stdout) { return "" }
    switch ($agent.transcript) {
        "claude-stream-json" {
            $last = ""
            foreach ($l in ($stdout -split "`n")) { try { $j = $l | ConvertFrom-Json; if ($j.type -eq "result" -and $j.result) { $last = $j.result } } catch { } }
            return $last
        }
        "gemini-stream-json" {
            $text = ""
            foreach ($l in ($stdout -split "`n")) { try { $j = $l | ConvertFrom-Json; if ($j.type -eq "message" -and $j.role -eq "assistant" -and $j.content) { $text += $j.content } } catch { } }
            return $text
        }
        "cursor-stream-json" {
            $last = ""
            foreach ($l in ($stdout -split "`n")) { try { $j = $l | ConvertFrom-Json; if ($j.type -eq "result" -and $j.result) { $last = $j.result } } catch { } }
            return $last
        }
        "codex-jsonl" {
            $last = ""
            foreach ($l in ($stdout -split "`n")) { try { $j = $l | ConvertFrom-Json; if ($j.item -and $j.item.type -eq "agent_message" -and $j.item.text) { $last = $j.item.text } } catch { } }
            return $last
        }
        default { return $stdout }
    }
}

function Test-SubagentUsed($agent, $stdout) {
    # Best effort, per transcript format: a delegation to the locator specialist.
    if ($agent.transcript -eq "none" -or -not $stdout) { return $false }
    return [bool]($stdout -match 'locator-specialist')
}

function Test-MainContextSnapshotRead($agent, $stdout, $snapshotName) {
    # Best effort: a file-read tool call on the snapshot in the MAIN context (Claude marks subagent calls with
    # parent_tool_use_id; other formats cannot distinguish, so any read counts).
    if ($agent.transcript -eq "none" -or -not $stdout) { return $null }
    foreach ($l in ($stdout -split "`n")) {
        if ($l -notmatch [regex]::Escape($snapshotName)) { continue }
        if ($agent.transcript -eq "claude-stream-json") {
            try {
                $j = $l | ConvertFrom-Json
                if ($j.type -eq "assistant" -and -not $j.parent_tool_use_id) {
                    foreach ($c in @($j.message.content)) { if ($c.type -eq "tool_use" -and $c.name -eq "Read" -and "$($c.input.file_path)" -match [regex]::Escape($snapshotName)) { return $true } }
                }
            } catch { }
        } elseif ($l -match '(read_file|readToolCall|"Read")') { return $true }
    }
    return $false
}

function Normalize-Locator([string]$s) {
    if (-not $s) { return "" }
    $t = $s.Trim()
    $t = $t -replace '^```[a-z]*\s*', '' -replace '\s*```$', ''
    $line = ($t -split "`n" | ForEach-Object { $_.Trim().Trim('`') } | Where-Object { $_ } | Select-Object -Last 1)
    if (-not $line) { return "" }
    $line = $line -replace '"', "'"
    $line = $line -replace '\s*=\s*', '=' -replace '\[\s+', '[' -replace '\s+\]', ']'
    return $line.Trim()
}

function Read-Trace($path) {
    if (-not (Test-Path $path)) { return @() }
    return @(Get-Content $path | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
}

# ---------- plan ----------
$plan = @()
foreach ($a in $agentIds) {
    $ag = $agentRows | Where-Object { $_.id -eq $a }
    foreach ($l in $langIds) {
        $lang = $langTable.languages | Where-Object { $_.id -eq $l }
        $probes = @([pscustomobject]@{ kind = "discovery"; domain = $null; target = $null })
        foreach ($d in $lang.domains) {
            $dom = $langTable.domains | Where-Object { $_.id -eq $d }
            $key = Get-Content (Join-Path $fixtureDir "$($dom.fixture).answers.json") -Raw | ConvertFrom-Json
            $ts = @($key.targets)
            if ($Targets -eq "1") { $ts = @($ts[0]) }
            foreach ($t in $ts) { $probes += [pscustomobject]@{ kind = "delegation"; domain = $dom; target = $t; key = $key } }
        }
        $plan += [pscustomobject]@{ agent = $ag; lang = $lang; probes = $probes }
    }
}
$calls = ($plan | ForEach-Object { $_.probes.Count } | Measure-Object -Sum).Sum

Write-Host "BELLATRIX agent conformance - agents: $($agentIds -join ', ') | languages: $($langIds -join ', ') | targets/domain: $Targets"
Write-Host "Planned agent calls: $calls (one headless prompt each)."
Write-Host "NOTE: real runs invoke the agents' models and MAY CONSUME PAID / METERED USAGE."
foreach ($ag in @($plan | ForEach-Object { $_.agent } | Group-Object id | ForEach-Object { $_.Group[0] })) { Write-Host "  cost [$($ag.id)]: $($ag.cost)" }

$discoveryPrompt = "List every skill and every subagent or custom agent available to you in this project whose name starts with bellatrix-. Do not run any commands. Reply with one JSON object only, with two arrays named skills and agents."
function Get-DelegationPrompt($dom, $target, $snapshotRel) {
    return "A BELLATRIX $($dom.id) UI snapshot of $($dom.app) is saved at $snapshotRel. Following this project's BELLATRIX instructions, find the locator for $($target.description). Reply with the locator only, nothing else."
}

if ($DryRun) {
    foreach ($item in $plan) {
        $bin = Resolve-Binary $item.agent
        $binText = $(if ($bin) { $bin } else { "($($item.agent.binaries -join '|') not installed -> SKIPPED-NOT-INSTALLED)" })
        Write-Host ""
        Write-Host "== $($item.agent.id) x $($item.lang.id)  [$binText]"
        Write-Host "   setup: git init; markers: $(@($item.lang.markerFiles.PSObject.Properties | ForEach-Object { $_.Name }) -join ', '); $Exe init --agent $($item.agent.id) --language $($item.lang.id) --domains $($item.lang.domains -join ',') --json"
        foreach ($f in @($item.agent.setupFiles.PSObject.Properties)) { Write-Host "   setup file: $($f.Name)" }
        foreach ($p in $item.probes) {
            if ($p.kind -eq "discovery") { $prompt = $discoveryPrompt }
            else { $prompt = Get-DelegationPrompt $p.domain $p.target ".bellatrix/snapshots/$($p.domain.fixture).json" }
            $argv = Expand-Args $item.agent.headless $prompt "<temp-repo>"
            $label = $(if ($p.kind -eq "discovery") { "discovery" } else { "delegation $($p.domain.id)/$($p.target.id)$(if ($p.key.synthetic) { ' [synthetic fixture]' })" })
            Write-Host "   [$label] $($item.agent.binaries[0]) $(($argv | ForEach-Object { Quote-Arg $_ }) -join ' ')"
        }
    }
    Write-Host ""
    Write-Host "Dry run: nothing was invoked. Re-run with -Confirm to execute $calls agent calls."
    exit 0
}

if (-not $Confirm) {
    Write-Host ""
    Write-Host "Refusing to run $calls agent calls without -Confirm (they may consume paid / metered usage). Use -DryRun to preview."
    exit 2
}

# The trace log is required for the delegation verdict.
$probeTrace = Join-Path ([IO.Path]::GetTempPath()) ("bellatrix-trace-probe-" + [guid]::NewGuid().ToString("N") + ".jsonl")
$env:BELLATRIX_TRACE = $probeTrace; & $Exe --version | Out-Null; Remove-Item Env:\BELLATRIX_TRACE
if (-not (Test-Path $probeTrace)) { throw "'$Exe' does not support BELLATRIX_TRACE (need bellatrix >= 3.0.1)." }
Remove-Item $probeTrace
$bellatrixVersion = ((& $Exe --version) | ConvertFrom-Json).version
# Agents call plain `bellatrix`: put the -Exe build first on their PATH so they run the same (trace-capable) binary.
$exeDir = Split-Path -Parent (Get-Command $Exe).Source
$agentPath = "$exeDir;$env:PATH"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
foreach ($item in $plan) {
    $ag = $item.agent; $lang = $item.lang
    $record = [ordered]@{
        agent = $ag.id; language = $lang.id; bellatrixVersion = $bellatrixVersion; agentVersion = $null
        status = "RAN"; startedAt = (Get-Date).ToUniversalTime().ToString("o"); discovery = $null; delegation = @(); notes = @()
    }
    $outFile = Join-Path $OutDir "$($ag.id)-$($lang.id).json"
    $bin = Resolve-Binary $ag
    $work = Join-Path ([IO.Path]::GetTempPath()) ("bellatrix-conformance-$($ag.id)-$($lang.id)-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    try {
        if (-not $bin) { $record.status = "SKIPPED-NOT-INSTALLED"; continue }
        $v = Invoke-Captured $bin @($ag.versionArgs) $work @{} 60
        $record.agentVersion = ("$($v.stdout)$($v.stderr)" -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1)
        if ($ag.capabilityProbe) {
            $cp = Invoke-Captured $bin @($ag.capabilityProbe.args) $work @{} 60
            if ("$($cp.stdout)$($cp.stderr)" -notmatch [regex]::Escape($ag.capabilityProbe.mustContain)) {
                $record.status = "SKIPPED-OUTDATED"; $record.notes += $ag.capabilityProbe.outdatedHint; continue
            }
        }
        if (-not (Test-Auth $ag $bin $work)) { $record.status = "SKIPPED-NOT-LOGGED-IN"; continue }

        # 1. temp repo
        & git -C $work init -q 2>$null | Out-Null
        foreach ($f in @($lang.markerFiles.PSObject.Properties)) { Set-Content -Path (Join-Path $work $f.Name) -Value $f.Value -Encoding UTF8 -NoNewline }
        foreach ($f in @($ag.setupFiles.PSObject.Properties)) {
            $target = Join-Path $work $f.Name; New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
            Set-Content -Path $target -Value $f.Value -Encoding UTF8 -NoNewline
        }
        $init = Invoke-Captured $Exe @("init", "--agent", $ag.id, "--language", $lang.id, "--domains", ($lang.domains -join ","), "--json") $work @{} 120
        if ($init.exitCode -ne 0) { throw "bellatrix init failed: $($init.stdout) $($init.stderr)" }

        # 2. discovery
        $expected = Get-Expected $work $ag.id
        $r = Invoke-Captured $bin (Expand-Args $ag.headless $discoveryPrompt $work) $work @{ PATH = $agentPath } $TimeoutSeconds
        $reply = Get-ReplyText $ag $r.stdout
        $foundSkills = @(); $foundAgents = @()
        if ($reply -match '(?s)\{.*\}') {
            try { $j = $Matches[0] | ConvertFrom-Json; $foundSkills = @($j.skills); $foundAgents = @($j.agents) } catch { }
        }
        $hitS = @($expected.skills | Where-Object { $foundSkills -contains $_ }).Count
        $hitA = @($expected.agents | Where-Object { $foundAgents -contains $_ }).Count
        $total = $expected.skills.Count + $expected.agents.Count
        $record.discovery = [ordered]@{
            expectedSkills = $expected.skills.Count; foundSkills = $hitS; expectedAgents = $expected.agents.Count; foundAgents = $hitA
            score = $(if ($total) { [math]::Round(($hitS + $hitA) / $total, 3) } else { 0 }); pass = (($hitS + $hitA) -eq $total -and $total -gt 0)
            missing = @(@($expected.skills | Where-Object { $foundSkills -notcontains $_ }) + @($expected.agents | Where-Object { $foundAgents -notcontains $_ }))
            exitCode = $r.exitCode; timedOut = $r.timedOut
        }

        # 3. delegation
        foreach ($p in ($item.probes | Where-Object { $_.kind -eq "delegation" })) {
            $snapDir = Join-Path $work ".bellatrix\snapshots"; New-Item -ItemType Directory -Force -Path $snapDir | Out-Null
            $snapName = "$($p.domain.fixture).json"
            Copy-Item (Join-Path $fixtureDir $snapName) (Join-Path $snapDir $snapName) -Force
            $trace = Join-Path $work ".bellatrix\trace-$($p.domain.id)-$($p.target.id).jsonl"
            $prompt = Get-DelegationPrompt $p.domain $p.target ".bellatrix/snapshots/$snapName"
            $r = Invoke-Captured $bin (Expand-Args $ag.headless $prompt $work) $work @{ BELLATRIX_TRACE = $trace; PATH = $agentPath } $TimeoutSeconds
            $reply = Get-ReplyText $ag $r.stdout
            $norm = Normalize-Locator $reply
            $expectedNorm = @($p.target.expected | ForEach-Object { Normalize-Locator $_ })
            $lines = @(Read-Trace $trace)
            $findUsed = [bool](@($lines | Where-Object { $_.verb -in @("find", "query-snapshot") -and ($_.args -contains "--snapshot") }).Count)
            $heavy = @($lines | Where-Object { $_.contextHeavy })
            $sub = Test-SubagentUsed $ag $r.stdout
            $mainRead = Test-MainContextSnapshotRead $ag $r.stdout $snapName
            $match = $expectedNorm -contains $norm
            $record.delegation += [ordered]@{
                domain = $p.domain.id; target = $p.target.id; synthetic = [bool]$p.key.synthetic
                reply = $reply; normalized = $norm; match = $match
                traceFind = $findUsed; subagent = $sub; contextHeavy = $heavy.Count; mainContextSnapshotRead = $mainRead
                traceLines = $lines.Count; exitCode = $r.exitCode; timedOut = $r.timedOut
                pass = ($match -and ($findUsed -or $sub) -and $heavy.Count -eq 0 -and $mainRead -ne $true)
            }
        }
    } catch {
        $record.status = "ERROR"; $record.notes += $_.Exception.Message
    } finally {
        $record.finishedAt = (Get-Date).ToUniversalTime().ToString("o")
        ($record | ConvertTo-Json -Depth 8) | Out-File -Encoding utf8 $outFile
        Write-Host ("{0,-8} x {1,-10} {2}" -f $ag.id, $lang.id, $record.status)
        Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    }
}
Write-Host ""
Write-Host "Results: $OutDir  -> aggregate with: powershell -File scripts/conformance-report.ps1"
