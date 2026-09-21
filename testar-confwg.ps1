<#
    Teste de ConfWg.Despir.

    Guarda o conserto de uma falha cara: o TunnelX.conf e escrito no formato do
    wireguard-windows (com Address, MTU, DNS), mas "wg syncconf" fala o
    protocolo, nao o wg-quick, e aborta na primeira dessas chaves. Como quem
    chamava caia num plano B que reinstalava o servico do tunel, e o timer de
    status rodava a cada segundo, o tunel se reinstalava em laco e derrubava
    todos os clientes de poucos em poucos segundos.

    Se alguem voltar a passar o arquivo cru ao syncconf, ou acrescentar uma
    diretiva nova ao [Interface] sem ensina-la aqui, este teste acusa.

    Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File testar-confwg.ps1
#>
$ErrorActionPreference = 'Continue'

$exe = Join-Path $PSScriptRoot 'tunnelx\bin\Debug\tunnelx.exe'
if (-not (Test-Path $exe)) { Write-Host "compile o projeto antes: $exe" -ForegroundColor Red; exit 1 }

$t = [System.Reflection.Assembly]::LoadFrom($exe).GetType('tunnelx.Services.ConfWg')
function Despir([string]$c) { $t.GetMethod('Despir').Invoke($null, @($c)) }

$falhas = 0
function Ok($nome, $cond) {
    if ($cond) { Write-Host "  ok    $nome" }
    else { Write-Host "  FALHA $nome" -ForegroundColor Red; $script:falhas++ }
}

$completo = @'
# comentario
[Interface]
PrivateKey = AAAA
Address = 10.66.66.1/16
ListenPort = 51820
MTU = 1420
DNS = 8.8.8.8
Table = off
PostUp = echo oi

[Peer]
PublicKey = BBBB
PresharedKey = CCCC
AllowedIPs = 10.66.66.2/32
Endpoint = 1.2.3.4:51820
PersistentKeepalive = 15

[Coisa]
PublicKey = NAODEVEPASSAR
'@

$r = Despir $completo

Ok 'tira Address'        (-not ($r -match 'Address'))
Ok 'tira MTU'            (-not ($r -match 'MTU'))
Ok 'tira DNS'            (-not ($r -match 'DNS'))
Ok 'tira Table'          (-not ($r -match 'Table'))
Ok 'tira PostUp'         (-not ($r -match 'PostUp'))
Ok 'tira comentario'     (-not ($r -match 'comentario'))
Ok 'mantem PrivateKey'   ($r -match 'PrivateKey = AAAA')
Ok 'mantem ListenPort'   ($r -match 'ListenPort = 51820')
Ok 'mantem PublicKey'    ($r -match 'PublicKey = BBBB')
Ok 'mantem PresharedKey' ($r -match 'PresharedKey = CCCC')
Ok 'mantem AllowedIPs'   ($r -match 'AllowedIPs = 10.66.66.2/32')
Ok 'mantem Endpoint'     ($r -match 'Endpoint = 1.2.3.4:51820')
Ok 'mantem Keepalive'    ($r -match 'PersistentKeepalive = 15')
Ok 'descarta secao desconhecida' (-not ($r -match 'NAODEVEPASSAR'))
Ok 'nao deixa [Coisa]'   (-not ($r -match 'Coisa'))
Ok 'vazio nao quebra'    ((Despir '') -eq '')
Ok 'um [Interface]'      (([regex]::Matches($r, '\[Interface\]')).Count -eq 1)
Ok 'um [Peer]'           (([regex]::Matches($r, '\[Peer\]')).Count -eq 1)
Ok 'aguenta CRLF'        ((Despir "[Interface]`r`nPrivateKey = X`r`nAddress = 1.1.1.1/32`r`n") -match 'PrivateKey = X')

# A prova que importa: o proprio wg.exe tem que aceitar o arquivo. Uma interface
# inexistente faz o parse acontecer e para antes de tocar em qualquer tunel.
$wg = 'C:\Program Files\WireGuard\wg.exe'
if (Test-Path $wg) {
    Write-Host ''
    Write-Host '-- o wg.exe de verdade --'
    $k = & $wg genkey; $p = & $wg genkey
    $conf = "[Interface]`nPrivateKey = $k`nAddress = 10.66.66.1/16`nListenPort = 51820`nMTU = 1420`n`n[Peer]`nPublicKey = $p`nAllowedIPs = 10.66.66.2/32`n"

    $arqCru = Join-Path $env:TEMP 'tx_teste_cru.conf'
    $arqOk  = Join-Path $env:TEMP 'tx_teste_despido.conf'
    Set-Content -Path $arqCru -Value $conf -Encoding ascii
    Set-Content -Path $arqOk  -Value (Despir $conf) -Encoding ascii

    $cru = (& $wg syncconf NenhumaInterfaceAqui $arqCru) 2>&1 | Out-String
    $ok  = (& $wg syncconf NenhumaInterfaceAqui $arqOk)  2>&1 | Out-String

    Ok 'o arquivo cru e recusado no parse (era o bug)' ($cru -match 'unrecognized|parsing error')
    Ok 'o arquivo despido passa no parse'              (-not ($ok -match 'unrecognized|parsing error'))
} else {
    Write-Host ''
    Write-Host '  (wg.exe nao instalado — pulei a prova com o binario real)' -ForegroundColor Yellow
}

Write-Host ''
if ($falhas -eq 0) { Write-Host 'Todos passaram' -ForegroundColor Green; exit 0 }
Write-Host "$falhas FALHA(S)" -ForegroundColor Red; exit 1
