<#
.SYNOPSIS
    Valida localmente os artefatos de homologacao Azure, sem provisionar recursos.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bicepFile = Join-Path $repoRoot 'infra/azure/main.bicep'
$workflowFile = Join-Path $repoRoot '.github/workflows/azure-hml.yml'
$spaConfig = Join-Path $repoRoot 'frontend/public/staticwebapp.config.json'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) nao encontrado no PATH.'
}

Write-Host 'Compilando Bicep...'
& az bicep build --file $bicepFile --stdout | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'az bicep build falhou.'
}

Write-Host 'Validando scripts PowerShell...'
Get-ChildItem (Join-Path $repoRoot 'scripts/azure') -Filter '*.ps1' | ForEach-Object {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        $messages = $errors | ForEach-Object { "$($_.Extent.StartLineNumber): $($_.Message)" }
        throw "Erro de sintaxe em $($_.Name):`n$($messages -join "`n")"
    }
}

Write-Host 'Validando YAML do workflow...'
& npx --yes js-yaml $workflowFile | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'YAML invalido no workflow.'
}

Write-Host 'Validando configuracao do Static Web Apps...'
Get-Content $spaConfig -Raw | ConvertFrom-Json | Out-Null

Write-Host 'Validacao local concluida. Nenhum recurso Azure foi criado.'
