# Runs a test battery against an Ollama model with the canonical NPC system prompt.
# Usage:  pwsh run_battery.ps1 -Cycle 1 -Model npc-qwen35-2b
param(
    [int]$Cycle = 1,
    [string]$Model = "npc-qwen35-2b",
    [string]$Endpoint = "http://127.0.0.1:11434/api/chat",
    [int]$TimeoutSec = 180,
    [switch]$LeanCut
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cycleDir = Join-Path $root ("cycle_{0:00}" -f $Cycle)
if (-not (Test-Path $cycleDir)) { New-Item -ItemType Directory -Path $cycleDir | Out-Null }

$systemPromptPath = Join-Path $cycleDir 'maren_system_prompt.txt'
if ($LeanCut) { $systemPromptPath = Join-Path $cycleDir 'maren_system_prompt_lean.txt' }
if (-not (Test-Path $systemPromptPath)) { throw "System prompt missing: $systemPromptPath" }

$systemPrompt = Get-Content $systemPromptPath -Raw
$battery = Get-Content (Join-Path $root 'battery.json') -Raw | ConvertFrom-Json

# JSON schema mirroring NpcLlmResponseParser.StructuredTurnDto.
$format = @{
    type = 'object'
    properties = @{
        trust     = @{ type='integer'; minimum=-3; maximum=3 }
        affection = @{ type='integer'; minimum=-3; maximum=3 }
        suspicion = @{ type='integer'; minimum=-3; maximum=3 }
        topic     = @{ type='string' }
        events    = @{ type='array'; items=@{ type='string' } }
        reply     = @{ type='string' }
    }
    required = @('trust','affection','suspicion','topic','events','reply')
}

$messages = New-Object System.Collections.Generic.List[object]
$messages.Add(@{ role='system'; content=$systemPrompt })

$transcript = @()
foreach ($turn in $battery.turns) {
    if ($LeanCut -and $turn.n -gt 5) { break }  # lean-cut only first 5 turns
    Write-Host ("[Turn {0}] {1}" -f $turn.n, $turn.input)

    $userMsg = @{ role='user'; content=$turn.input }
    $messages.Add($userMsg)

    $body = @{
        model      = $Model
        messages   = $messages
        stream     = $false
        format     = $format
        keep_alive = '30m'
        options    = @{ temperature = 0.7; top_p = 0.8; top_k = 20; num_ctx = 8192 }
    } | ConvertTo-Json -Depth 10 -Compress

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $body -ContentType 'application/json' -TimeoutSec $TimeoutSec
        $sw.Stop()
        $rawAssistant = $resp.message.content
        $assistantMsg = @{ role='assistant'; content=$rawAssistant }
        $messages.Add($assistantMsg)

        $parsed = $null
        try { $parsed = $rawAssistant | ConvertFrom-Json } catch { $parsed = $null }

        $rec = [PSCustomObject]@{
            n          = $turn.n
            input      = $turn.input
            intent     = $turn.intent
            elapsed_ms = $sw.ElapsedMilliseconds
            raw        = $rawAssistant
            parsed     = $parsed
            error      = $null
        }
    } catch {
        $sw.Stop()
        $rec = [PSCustomObject]@{
            n          = $turn.n
            input      = $turn.input
            intent     = $turn.intent
            elapsed_ms = $sw.ElapsedMilliseconds
            raw        = $null
            parsed     = $null
            error      = $_.ToString()
        }
        # On error remove the unanswered user message so history stays clean
        $messages.RemoveAt($messages.Count - 1)
    }

    $transcript += $rec
    if ($rec.parsed -and $rec.parsed.reply) {
        Write-Host ("  -> {0}" -f $rec.parsed.reply.Substring(0, [Math]::Min(120, $rec.parsed.reply.Length))) -ForegroundColor Cyan
        Write-Host ("  [trust {0:+#;-#;0} aff {1:+#;-#;0} susp {2:+#;-#;0} topic={3} events={4} elapsed={5}ms]" `
            -f $rec.parsed.trust, $rec.parsed.affection, $rec.parsed.suspicion, $rec.parsed.topic, ($rec.parsed.events -join ','), $rec.elapsed_ms) -ForegroundColor DarkGray
    } elseif ($rec.error) {
        Write-Host ("  !! {0}" -f $rec.error) -ForegroundColor Red
    } else {
        Write-Host ("  ?? raw: {0}" -f $rec.raw) -ForegroundColor Yellow
    }
}

$outPath = Join-Path $cycleDir ($(if ($LeanCut) {'transcript_lean.json'} else {'transcript.json'}))
$transcript | ConvertTo-Json -Depth 10 | Set-Content -Path $outPath -Encoding utf8

# Aggregate stats
$valid = ($transcript | Where-Object { $_.parsed -ne $null }).Count
$errs  = ($transcript | Where-Object { $_.error -ne $null }).Count
$avgMs = ($transcript | Measure-Object -Property elapsed_ms -Average).Average
Write-Host "`n--- Summary ---"
Write-Host ("Turns total : {0}" -f $transcript.Count)
Write-Host ("Valid JSON  : {0}" -f $valid)
Write-Host ("Errors      : {0}" -f $errs)
Write-Host ("Avg ms/turn : {0:N0}" -f $avgMs)
Write-Host ("Saved       : {0}" -f $outPath)
