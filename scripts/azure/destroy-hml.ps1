<#
.SYNOPSIS
    Remove o resource group de homologacao.

.DESCRIPTION
    Exclusivo de homologacao. Nao use contra resource groups de producao.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) nao encontrado no PATH.'
}

$exists = & az group exists --name $ResourceGroup
if ($LASTEXITCODE -ne 0) {
    throw 'Falha ao consultar o resource group. Verifique o login Azure.'
}

if ($exists -ne 'true') {
    Write-Host "Resource group $ResourceGroup nao existe."
    return
}

if ($PSCmdlet.ShouldProcess($ResourceGroup, 'Excluir resource group de homologacao')) {
    & az group delete --name $ResourceGroup --yes --no-wait
    if ($LASTEXITCODE -ne 0) {
        throw 'Falha ao solicitar a exclusao do resource group.'
    }
    Write-Host "Exclusao de $ResourceGroup solicitada. O Key Vault mantem soft delete conforme a politica do ambiente."
}
