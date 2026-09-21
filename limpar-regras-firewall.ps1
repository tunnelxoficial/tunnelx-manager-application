<#
    Tira as regras de firewall duplicadas que o TunnelX acumulou.

    Por que existiam: BlockClientInternet habilitava as regras ja existentes e
    ainda assim acrescentava outro par, em toda chamada. Quem chamava era
    RestoreAfterTunnelRestart, que rodava a cada poucos segundos por causa do
    laco de reinstalacao do tunel. Resultado: dois registros novos por cliente
    bloqueado, a cada volta, sem fim. O firewall percorre essa lista a cada
    conexao nova, entao ela cobra caro no desempenho.

    O que este script faz: mantem UMA regra de cada nome e apaga as repetidas.
    Nao desbloqueia ninguem — quem estava bloqueado continua bloqueado.

    Rode como Administrador, no servidor. Sem -Aplicar ele so mostra o que faria.
#>
[CmdletBinding()]
param([switch]$Aplicar)

$regras = @(Get-NetFirewallRule -DisplayName 'TunnelX BLOCK *' -ErrorAction SilentlyContinue)

if ($regras.Count -eq 0) { Write-Host 'Nenhuma regra TunnelX BLOCK encontrada.' ; return }

Write-Host ("Regras TunnelX BLOCK no firewall: {0}" -f $regras.Count)

$grupos    = $regras | Group-Object DisplayName
$repetidas = $grupos | Where-Object { $_.Count -gt 1 }

Write-Host ("Nomes distintos: {0}" -f $grupos.Count)
Write-Host ("Nomes com repeticao: {0}" -f $repetidas.Count)

$paraApagar = foreach ($g in $repetidas) { $g.Group | Select-Object -Skip 1 }
$paraApagar = @($paraApagar)

Write-Host ("A apagar: {0}   |   a manter: {1}" -f $paraApagar.Count, $grupos.Count)

if ($repetidas.Count -gt 0) {
    Write-Host ''
    Write-Host 'Piores casos:'
    $repetidas | Sort-Object Count -Descending | Select-Object -First 10 |
        Format-Table @{n='copias';e={$_.Count}}, Name -AutoSize | Out-String | Write-Host
}

if (-not $Aplicar) {
    Write-Host 'Simulacao. Repita com  -Aplicar  para apagar de verdade.'
    return
}

$n = 0
foreach ($r in $paraApagar) {
    try { Remove-NetFirewallRule -Name $r.Name -ErrorAction Stop; $n++ }
    catch { Write-Warning ("nao consegui apagar {0}: {1}" -f $r.DisplayName, $_.Exception.Message) }
}

Write-Host ("Apagadas: {0}" -f $n)
Write-Host ("Restaram: {0}" -f @(Get-NetFirewallRule -DisplayName 'TunnelX BLOCK *' -ErrorAction SilentlyContinue).Count)
