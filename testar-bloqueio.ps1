<#
    Testa as pecas do corte de internet, SEM encostar em tunel nenhum.

    Tudo aqui e funcao pura ou disco em pasta temporaria: nenhum wg.exe e
    executado, nenhum peer e tocado. Pode rodar em qualquer maquina.

    O que esta coberto e o que decide quem fica sem internet:

      ChavesDoStatus        le a saida do "wg show". Um engano aqui faz a
                            reconciliacao achar que um peer sumiu e recria-lo, ou
                            que ele existe e nunca corta o inadimplente.

      DefinirEnabledNaPasta grava o "enabled" no client.json. E o que faz o corte
                            sobreviver ao restart do tunel. Antes a versao
                            equivalente trocava a linha SEM a virgula, produzindo
                            JSON invalido colado no campo seguinte.

    Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File testar-bloqueio.ps1
#>
$ErrorActionPreference = 'Continue'

$exe = Join-Path $PSScriptRoot 'tunnelx\bin\Debug\tunnelx.exe'
if (-not (Test-Path $exe)) { Write-Host "compile o projeto antes: $exe" -ForegroundColor Red; exit 1 }

$tm = [System.Reflection.Assembly]::LoadFrom($exe).GetType('tunnelx.Services.TunnelManager')

$falhas = 0
function Ok($nome, $cond) {
    if ($cond) { Write-Host "  ok    $nome" }
    else { Write-Host "  FALHA $nome" -ForegroundColor Red; $script:falhas++ }
}

# ---------------------------------------------------------- ChavesDoStatus --
Write-Host '-- ChavesDoStatus (parse do wg show) --'

$parse = $tm.GetMethod('ChavesDoStatus')
function Chaves([string]$txt) { $parse.Invoke($null, @($txt)) }

$saida = @"
interface: TunnelX
  public key: SERVIDOR123=
  private key: (hidden)
  listening port: 51820

peer: AAAA1111=
  endpoint: 189.1.2.3:44444
  allowed ips: 10.66.66.2/32
  latest handshake: 1 minute, 4 seconds ago
  transfer: 1.02 MiB received, 8.55 MiB sent

peer: BBBB2222=
  allowed ips: 10.66.66.3/32
  persistent keepalive: every 15 seconds
"@

$c = Chaves $saida
Ok 'achou os dois peers'            ($c.Count -eq 2)
Ok 'pegou a primeira chave'         ($c.Contains('AAAA1111='))
Ok 'pegou a segunda chave'          ($c.Contains('BBBB2222='))
Ok 'NAO confundiu com a do servidor' (-not $c.Contains('SERVIDOR123='))

Ok 'saida vazia nao quebra'         ((Chaves '').Count -eq 0)
Ok 'nulo nao quebra'                ((Chaves $null).Count -eq 0)
Ok 'sem peer nenhum'                ((Chaves "interface: TunnelX`n  listening port: 51820").Count -eq 0)
Ok 'aguenta CRLF'                   ((Chaves "peer: XYZ=`r`n  allowed ips: 10.66.66.9/32`r`n").Contains('XYZ='))
Ok 'ignora chave vazia'             ((Chaves "peer: `npeer: OK=").Count -eq 1)

# ------------------------------------------------- DefinirEnabledNaPasta ----
Write-Host ''
Write-Host '-- DefinirEnabledNaPasta (grava no client.json) --'

$definir = $tm.GetMethod('DefinirEnabledNaPasta')
function Definir([string]$pasta, [bool]$v) { $definir.Invoke($null, @($pasta, $v)) }

$raiz = Join-Path $env:TEMP ("tx_bloq_" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $raiz | Out-Null

function NovaPasta([string]$conteudo) {
    $p = Join-Path $raiz ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $p | Out-Null
    Set-Content -Path (Join-Path $p 'client.json') -Value $conteudo -Encoding utf8
    return $p
}

function LerJson([string]$pasta) {
    Get-Content (Join-Path $pasta 'client.json') -Raw
}

# O formato que DeviceProvisioner e DbBackgroundService realmente gravam:
# enabled NO MEIO do objeto, com virgula, seguido de created_at.
$comVirgula = @'
{
  "nome": "LUCAS",
  "publicKey": "AAAA1111=",
  "address": "10.66.66.2/32",
  "enabled": true,
  "created_at": "2026-09-01T10:00:00Z"
}
'@

$p = NovaPasta $comVirgula
Ok 'cortar devolve true'      ((Definir $p $false) -eq $true)

$txt = LerJson $p
Ok 'gravou enabled false'     ($txt -match '"enabled":\s*false')
Ok 'PRESERVOU a virgula'      ($txt -match '"enabled":\s*false,')
Ok 'nao perdeu created_at'    ($txt -match 'created_at')

$valido = $true
try { $obj = $txt | ConvertFrom-Json } catch { $valido = $false }
Ok 'continua JSON VALIDO'     $valido
if ($valido) {
    Ok '  enabled = false no objeto'  ($obj.enabled -eq $false)
    Ok '  publicKey intacta'          ($obj.publicKey -eq 'AAAA1111=')
    Ok '  address intacto'            ($obj.address -eq '10.66.66.2/32')
}

Ok 'repetir nao regrava'      ((Definir $p $false) -eq $false)
Ok 'devolver o acesso grava'  ((Definir $p $true) -eq $true)

$txt2 = LerJson $p
Ok 'voltou para true'         ($txt2 -match '"enabled":\s*true,')
$valido2 = $true
try { $null = $txt2 | ConvertFrom-Json } catch { $valido2 = $false }
Ok 'ainda JSON valido'        $valido2

# enabled como ULTIMO campo, sem virgula: nao pode ganhar uma.
$semVirgula = @'
{
  "publicKey": "BBBB2222=",
  "enabled": true
}
'@
$p2 = NovaPasta $semVirgula
$null = Definir $p2 $false
$t2 = LerJson $p2
Ok 'ultimo campo nao ganha virgula' ($t2 -notmatch '"enabled":\s*false,')
$v2 = $true; try { $null = $t2 | ConvertFrom-Json } catch { $v2 = $false }
Ok 'ultimo campo: JSON valido'      $v2

# Sem o campo: tem que ser inserido, e continuar valido.
$semCampo = @'
{
  "publicKey": "CCCC3333=",
  "address": "10.66.66.4/32"
}
'@
$p3 = NovaPasta $semCampo
Ok 'insere o campo que faltava'  ((Definir $p3 $false) -eq $true)
$t3 = LerJson $p3
$v3 = $true; try { $o3 = $t3 | ConvertFrom-Json } catch { $v3 = $false }
Ok 'insercao: JSON valido'       $v3
if ($v3) { Ok '  enabled = false'  ($o3.enabled -eq $false) }

# Pasta sem client.json: nao pode explodir.
$p4 = Join-Path $raiz 'vazia'
New-Item -ItemType Directory -Path $p4 | Out-Null
Ok 'pasta sem client.json devolve false' ((Definir $p4 $false) -eq $false)

Remove-Item -Recurse -Force $raiz -ErrorAction SilentlyContinue

Write-Host ''
if ($falhas -eq 0) { Write-Host 'Todos passaram' -ForegroundColor Green; exit 0 }
Write-Host "$falhas FALHA(S)" -ForegroundColor Red; exit 1
