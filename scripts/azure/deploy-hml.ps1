<#
.SYNOPSIS
    Provisiona e publica o ambiente de homologacao Azure da operacao P0.

.DESCRIPTION
    Este script cria um resource group, implanta o Bicep, publica a API, aplica
    migracoes/provisiona contas sinteticas e publica a SPA. Ele se recusa a
    executar sem login Azure e exige a confirmacao literal HOMOLOGACAO.

    O procedimento e exclusivo de homologacao. Nao use em producao.

.PARAMETER ResourceGroup
    Resource group que sera criado/atualizado.

.PARAMETER Location
    Regiao do App Service e do Azure SQL.

.PARAMETER SwaLocation
    Regiao do Azure Static Web Apps.

.PARAMETER NamePrefix
    Prefixo dos nomes dos recursos.

.PARAMETER SqlAdminLogin
    Login administrativo inicial do Azure SQL.

.PARAMETER SqlAdminPassword
    Senha administrativa inicial do Azure SQL.

.PARAMETER ValetPassword
    Senha da conta sintetica de manobrista.

.PARAMETER CashierPassword
    Senha da conta sintetica de caixa.

.PARAMETER AdminPassword
    Senha da conta sintetica de administrador.

.PARAMETER Force
    Pula a confirmacao interativa. Use apenas em automacao aprovada.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [string] $Location = 'brazilsouth',
    [string] $SwaLocation = 'eastus2',
    [string] $NamePrefix = 'estac-hml',

    [Parameter(Mandatory = $true)]
    [string] $SqlAdminLogin,

    [Parameter(Mandatory = $true)]
    [securestring] $SqlAdminPassword,

    [Parameter(Mandatory = $true)]
    [securestring] $ValetPassword,

    [Parameter(Mandatory = $true)]
    [securestring] $CashierPassword,

    [Parameter(Mandatory = $true)]
    [securestring] $AdminPassword,

    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bicepFile = Join-Path $repoRoot 'infra/azure/main.bicep'
$apiProject = Join-Path $repoRoot 'backend/src/Estacionamento.Api/Estacionamento.Api.csproj'
$frontendRoot = Join-Path $repoRoot 'frontend'
$artifacts = Join-Path $repoRoot '.artifacts/azure-hml'

function Invoke-Az {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
    & az @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "az $($Arguments -join ' ') falhou com codigo $LASTEXITCODE."
    }
}

function ConvertTo-PlainText {
    param([securestring] $Value)
    return [System.Net.NetworkCredential]::new('', $Value).Password
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) nao encontrado no PATH.'
}

& az account show --query id -o tsv 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Nenhuma sessao Azure ativa. Execute "az login" (com a conta/tenant que possui a assinatura) antes de continuar.'
}

& az extension add --name staticwebapp --only-show-errors 2>$null | Out-Null

if (-not $Force) {
    $confirmation = Read-Host 'Este script cria recursos Azure faturáveis de HOMOLOGACAO. Digite HOMOLOGACAO para continuar'
    if ($confirmation -ne 'HOMOLOGACAO') {
        throw 'Confirmacao invalida. Nenhum recurso foi criado.'
    }
}

if (-not (Test-Path $bicepFile)) {
    throw "Template Bicep nao encontrado: $bicepFile"
}

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$parametersFile = Join-Path $artifacts 'parameters.json'
$apiZip = Join-Path $artifacts 'api.zip'
$apiPublish = Join-Path $artifacts 'api'
$keyRing = Join-Path $artifacts 'key-ring'

$plainSqlAdminPassword = ConvertTo-PlainText $SqlAdminPassword
$plainValetPassword = ConvertTo-PlainText $ValetPassword
$plainCashierPassword = ConvertTo-PlainText $CashierPassword
$plainAdminPassword = ConvertTo-PlainText $AdminPassword

if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    throw 'ResourceGroup nao pode ser vazio.'
}

if ($NamePrefix -notmatch '^[A-Za-z0-9-]{3,20}$' -or $NamePrefix.StartsWith('-') -or $NamePrefix.EndsWith('-')) {
    throw 'NamePrefix deve ter de 3 a 20 caracteres alfanumericos ou hifens, sem hifen no inicio ou no fim.'
}

if ([string]::IsNullOrWhiteSpace($SqlAdminLogin) -or $SqlAdminLogin.Length -gt 128 -or $SqlAdminLogin -match '[;=\r\n]') {
    throw 'SqlAdminLogin deve ter de 1 a 128 caracteres e nao pode conter ";", "=" ou quebra de linha.'
}

if ($plainSqlAdminPassword.Length -lt 12 -or $plainSqlAdminPassword.Length -gt 128 -or $plainSqlAdminPassword -match '[;''"\r\n]') {
    throw 'SqlAdminPassword deve ter de 12 a 128 caracteres e nao pode conter ";", aspas ou quebra de linha.'
}

if ($plainValetPassword.Length -lt 12 -or $plainCashierPassword.Length -lt 12 -or $plainAdminPassword.Length -lt 12) {
    throw 'As tres senhas sinteticas devem ter ao menos 12 caracteres.'
}

try {
    Write-Host "Criando resource group $ResourceGroup em $Location..."
    Invoke-Az group create --name $ResourceGroup --location $Location --output none

    $clientIp = (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 20).Trim()
    Write-Host "IP do executor para o firewall do SQL: $clientIp"

    $parameters = @{
        '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
        contentVersion = '1.0.0.0'
        parameters = @{
            namePrefix = @{ value = $NamePrefix }
            location = @{ value = $Location }
            swaLocation = @{ value = $SwaLocation }
            sqlAdminLogin = @{ value = $SqlAdminLogin }
            sqlAdminPassword = @{ value = $plainSqlAdminPassword }
            valetPassword = @{ value = $plainValetPassword }
            cashierPassword = @{ value = $plainCashierPassword }
            adminPassword = @{ value = $plainAdminPassword }
            deployClientIp = @{ value = $clientIp }
        }
    }
    $parameters | ConvertTo-Json -Depth 6 | Set-Content -Path $parametersFile -Encoding utf8

    $deploymentName = "hml-$(Get-Date -Format 'yyyyMMddHHmmss')"
    Write-Host "Implantando Bicep ($deploymentName)..."
    $deploymentJson = Invoke-Az deployment group create `
        --resource-group $ResourceGroup `
        --name $deploymentName `
        --template-file $bicepFile `
        --parameters $parametersFile `
        --output json
    $deployment = $deploymentJson | ConvertFrom-Json
    $outputs = $deployment.properties.outputs

    $apiAppName = $outputs.apiAppName.value
    $apiUrl = $outputs.apiUrl.value
    $spaName = $outputs.spaName.value
    $spaUrl = $outputs.spaUrl.value
    $sqlServerName = $outputs.sqlServerName.value
    $sqlServerFqdn = $outputs.sqlServerFqdn.value
    $databaseName = $outputs.databaseName.value

    Write-Host "Publicando API $apiAppName..."
    if (Test-Path $apiPublish) { Remove-Item -Recurse -Force $apiPublish }
    if (Test-Path $apiZip) { Remove-Item -Force $apiZip }
    & dotnet publish $apiProject -c Release -o $apiPublish --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish da API falhou.' }

    Compress-Archive -Path (Join-Path $apiPublish '*') -DestinationPath $apiZip -Force
    Invoke-Az webapp deploy `
        --resource-group $ResourceGroup `
        --name $apiAppName `
        --src-path $apiZip `
        --type zip `
        --clean true `
        --output none

    Write-Host 'Aplicando migracoes e provisionando contas sinteticas...'
    $connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Persist Security Info=False;User ID=$SqlAdminLogin;Password=$plainSqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    New-Item -ItemType Directory -Force -Path $keyRing | Out-Null
    $previousEnvironment = @{
        ASPNETCORE_ENVIRONMENT = $env:ASPNETCORE_ENVIRONMENT
        ConnectionStrings__Identity = $env:ConnectionStrings__Identity
        DataProtection__KeyRingPath = $env:DataProtection__KeyRingPath
        Security__SpaOrigin = $env:Security__SpaOrigin
        SyntheticAccounts__ValetPassword = $env:SyntheticAccounts__ValetPassword
        SyntheticAccounts__CashierPassword = $env:SyntheticAccounts__CashierPassword
        SyntheticAccounts__AdministratorPassword = $env:SyntheticAccounts__AdministratorPassword
    }

    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ConnectionStrings__Identity = $connectionString
        $env:DataProtection__KeyRingPath = $keyRing
        $env:Security__SpaOrigin = $spaUrl
        $env:SyntheticAccounts__ValetPassword = $plainValetPassword
        $env:SyntheticAccounts__CashierPassword = $plainCashierPassword
        $env:SyntheticAccounts__AdministratorPassword = $plainAdminPassword

        & dotnet run --project $apiProject --configuration Release --no-launch-profile -- --provision-synthetic-accounts
        if ($LASTEXITCODE -ne 0) { throw 'Bootstrap de migracao/contas sinteticas falhou.' }
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            if ($null -eq $previousEnvironment[$name]) {
                Remove-Item -Path "Env:$name" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item -Path "Env:$name" -Value $previousEnvironment[$name]
            }
        }
    }

    Write-Host 'Publicando SPA...'
    Push-Location $frontendRoot
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci falhou.' }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'npm run build falhou.' }
    }
    finally {
        Pop-Location
    }

    $spaDist = Join-Path $frontendRoot 'dist/frontend/browser'
    if (-not (Test-Path $spaDist)) { throw "Build da SPA nao encontrado em $spaDist." }
    $deploymentToken = Invoke-Az staticwebapp secrets list --name $spaName --resource-group $ResourceGroup --query properties.apiKey -o tsv
    & npx --yes --package @azure/static-web-apps-cli swa deploy $spaDist --deployment-token $deploymentToken --env production
    if ($LASTEXITCODE -ne 0) { throw 'Deploy da SPA falhou.' }

    Write-Host 'Reiniciando a API para aplicar a configuracao...'
    Invoke-Az webapp restart --resource-group $ResourceGroup --name $apiAppName --output none

    Write-Host 'Executando smoke tests...'
    $antiforgery = $null
    $lastAntiforgeryError = $null

    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$spaUrl/api/v1/session/antiforgery" -UseBasicParsing -TimeoutSec 30
            $payload = $response.Content | ConvertFrom-Json
            if ($payload.requestToken) {
                $antiforgery = $payload
                break
            }

            $lastAntiforgeryError = 'resposta sem requestToken'
        }
        catch {
            $lastAntiforgeryError = $_.Exception.Message
        }

        if ($attempt -lt 8) {
            Start-Sleep -Seconds 10
        }
    }

    if ($null -eq $antiforgery) {
        throw "API via SPA nao retornou um token antiforgery valido. Ultimo erro: $lastAntiforgeryError"
    }

    $directStatus = $null
    try {
        $directResponse = Invoke-WebRequest -Uri "$apiUrl/healthz" -UseBasicParsing -TimeoutSec 30
        $directStatus = [int]$directResponse.StatusCode
    }
    catch {
        $httpResponse = $_.Exception.Response
        if ($null -eq $httpResponse) {
            throw
        }

        $directStatus = [int]$httpResponse.StatusCode
    }

    if ($directStatus -lt 400) {
        throw "Acesso direto a API respondeu HTTP $directStatus; o linked backend nao bloqueou o acesso."
    }

    Write-Host "Acesso direto a API bloqueado como esperado (HTTP $directStatus)."

    Write-Host ''
    Write-Host 'Homologacao publicada.'
    Write-Host "SPA: $spaUrl"
    Write-Host "API: $apiUrl (deve permanecer bloqueada fora do linked backend)"
    Write-Host "Resource group: $ResourceGroup"
    Write-Host 'Execute o E2E no dominio publicado e registre as evidencias antes de qualquer aprovacao.'
}
finally {
    if ($sqlServerName) {
        & az sql server firewall-rule delete `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name AllowDeployClient `
            --output none 2>$null
    }
    if (Test-Path $keyRing) { Remove-Item -Recurse -Force $keyRing -ErrorAction SilentlyContinue }
    if (Test-Path $parametersFile) { Remove-Item -Force $parametersFile }
    $plainSqlAdminPassword = $null
    $plainValetPassword = $null
    $plainCashierPassword = $null
    $plainAdminPassword = $null
}
