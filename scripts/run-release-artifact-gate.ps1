$ErrorActionPreference = "Continue"

Write-Host "== Smart Performance Doctor v42 Release Artifact Gate ==" -ForegroundColor Cyan

$out = ".\artifacts\release-gate"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$steps = @(
    @{ name = "Source verification"; command = ".\scripts\verify-source.ps1" },
    @{ name = "Design verification"; command = ".\scripts\verify-design-language.ps1" },
    @{ name = "XAML hardening"; command = ".\scripts\verify-xaml-hardening.ps1" },
    @{ name = "Dashboard binding"; command = ".\scripts\verify-dashboard-binding.ps1" },
    @{ name = "Core bridge"; command = ".\scripts\verify-core-dashboard-bridge.ps1" },
    @{ name = "Progress stream"; command = ".\scripts\verify-progress-stream.ps1" },
    @{ name = "Repair intelligence"; command = ".\scripts\verify-repair-intelligence.ps1" },
    @{ name = "RepairHelper E2E"; command = ".\scripts\verify-repairhelper-e2e-gate.ps1" },
    @{ name = "Stable log layout"; command = ".\scripts\verify-stable-log-layout.ps1" },
    @{ name = "Release artifacts"; command = ".\scripts\verify-release-artifacts.ps1" },
    @{ name = "Release artifact manifest"; command = ".\scripts\new-release-artifact-manifest.ps1" },
    @{ name = "Update channel"; command = ".\scripts\new-update-channel.ps1" }
)

$results = @()
foreach ($step in $steps) {
    Write-Host "`n== $($step.name) ==" -ForegroundColor Cyan
    try {
        Invoke-Expression $step.command
        $results += [PSCustomObject]@{ name = $step.name; status = "PASS"; command = $step.command; message = "" }
    } catch {
        Write-Host "[FAIL] $($step.name): $_" -ForegroundColor Red
        $results += [PSCustomObject]@{ name = $step.name; status = "FAIL"; command = $step.command; message = "$_" }
    }
}

$results | ConvertTo-Json -Depth 6 | Set-Content "$out\RELEASE_GATE_RESULTS_v42.json" -Encoding UTF8
Write-Host "[OK] Release gate result: $out\RELEASE_GATE_RESULTS_v42.json" -ForegroundColor Green
