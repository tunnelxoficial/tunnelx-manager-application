using System;
using System.Data.SqlClient;
using System.IO;

namespace tunnelx.Services
{
    /// <summary>
    /// Exclusao de um cliente pelo operador, a partir da grade.
    /// </summary>
    /// <remarks>
    /// Excluir tem que valer nos TRES lugares onde a conexao existe, e nessa ordem:
    ///
    ///   1. no banco  — senao o aplicativo do cliente continua anunciando o tunel
    ///                  como pronto, com um .conf que nao conecta mais;
    ///   2. no tunel  — senao a pessoa continua navegando mesmo sumindo da tela;
    ///   3. no disco  — senao o peer volta no proximo WriteServerConfFromClients,
    ///                  que monta o TunnelX.conf a partir das pastas de cliente.
    ///
    /// Tirar so da tela seria o pior resultado possivel: o operador acreditaria ter
    /// cortado o acesso, e a pessoa seguiria conectada. Este produto ja teve esse
    /// vazamento antes, por revogar no banco sem remover o peer.
    ///
    /// Para o modelo novo (pastas dev_&lt;id&gt;) a marca no banco e o que importa: o
    /// proprio ciclo do provisionador (RemoverRevogados) faz a limpeza de forma
    /// idempotente. Aqui a limpeza tambem e feita na hora, para o operador ver o
    /// resultado sem esperar ate um minuto.
    /// </remarks>
    public static class Exclusao
    {
        public class Resultado
        {
            public bool Excluiu;
            public string Mensagem;

            /// <summary>Verdadeiro quando a conexao tambem foi revogada no banco.</summary>
            public bool AvisouOSistema;
        }

        /// <summary>
        /// Remove o peer do tunel, apaga a pasta e, quando o cliente veio do
        /// sistema, marca a revogacao no banco.
        /// </summary>
        public static Resultado Excluir(ConnectionSnapshot.PeerRow peer)
        {
            if (peer == null || string.IsNullOrWhiteSpace(peer.PublicKey))
                return new Resultado { Excluiu = false, Mensagem = "Conexao invalida." };

            var r = new Resultado();

            // 1. Banco primeiro. Se o peer for removido e a marca falhar, o ciclo
            //    seguinte do provisionador enxerga a linha ainda ativa e nao
            //    reconstroi nada (ela esta CREATED), mas o aplicativo do cliente
            //    continuaria mostrando um tunel pronto. Marcando antes, o pior caso
            //    e o inverso: marcado no banco e ainda no tunel, que o proprio
            //    RemoverRevogados conserta no proximo ciclo.
            var deviceId = IdDoAparelho(peer);
            if (deviceId > 0)
            {
                r.AvisouOSistema = MarcarRevogado(deviceId);
                if (!r.AvisouOSistema)
                {
                    return new Resultado
                    {
                        Excluiu = false,
                        Mensagem = "Nao foi possivel falar com o banco para revogar o acesso. " +
                                   "Nada foi removido — tente de novo quando a conexao voltar."
                    };
                }
            }

            // 2. Tunel.
            if (!TunnelManager.RemovePeer(peer.PublicKey))
            {
                return new Resultado
                {
                    Excluiu = false,
                    AvisouOSistema = r.AvisouOSistema,
                    Mensagem = deviceId > 0
                        ? "O acesso foi revogado no sistema, mas o peer nao saiu do tunel agora. " +
                          "O provisionador vai remover no proximo ciclo."
                        : "Nao foi possivel remover o peer do tunel. Nada foi apagado."
                };
            }

            // O corte real e a saida do peer do tunel (RemovePeer, acima). Aqui havia
            // uma chamada a BlockClientInternet, que criava regras de firewall do
            // Windows — e nunca bloqueou nada: o firewall filtra o que TERMINA na
            // maquina, e o trafego do cliente e roteado/NATeado. So acumulava regras.

            // 3. Disco, e so entao reescreve o TunnelX.conf — ele e montado a partir
            //    do que sobrou nas pastas.
            var apagou = ApagarPasta(peer);
            TunnelManager.WriteServerConfFromClients();

            r.Excluiu = true;
            r.Mensagem = !apagou
                ? "Peer removido do tunel, mas a pasta do cliente nao pode ser apagada. " +
                  "Confira " + TunnelManager.ClientsDir + "."
                : deviceId > 0
                    ? "Conexao excluida e acesso revogado no sistema."
                    : "Conexao excluida do tunel e do disco.";

            Log.Info($"operador excluiu a conexao {peer.Name} ({peer.PublicKey.Substring(0, 8)}...)" +
                     (deviceId > 0 ? $", aparelho {deviceId}" : ", cliente sem cadastro"));

            return r;
        }

        /// <summary>
        /// Id do aparelho, quando a pasta veio do provisionamento novo (dev_&lt;id&gt;).
        /// </summary>
        /// <returns>Zero para cliente gerado a mao ou do modelo antigo.</returns>
        private static int IdDoAparelho(ConnectionSnapshot.PeerRow peer)
        {
            var pasta = peer.Pasta;
            if (string.IsNullOrEmpty(pasta)) return 0;

            var nome = Path.GetFileName(pasta);
            if (!nome.StartsWith("dev_", StringComparison.OrdinalIgnoreCase)) return 0;

            int id;
            return int.TryParse(nome.Substring(4), out id) ? id : 0;
        }

        private static bool MarcarRevogado(int deviceId)
        {
            try
            {
                using (var conn = new SqlConnection(DbBackgroundService.StringDeConexao))
                {
                    conn.Open();

                    // Nao apaga a linha: o registro precisa existir para o ciclo saber
                    // que ha peer a remover. E o mesmo motivo de revoked_at existir.
                    var sql = @"UPDATE ConnectionDevices
                                   SET revoked_at = SYSDATETIMEOFFSET(),
                                       updatedAt  = SYSDATETIMEOFFSET()
                                 WHERE id = @id
                                   AND revoked_at IS NULL";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", deviceId);

                        // Zero linhas tambem e sucesso: significa que ja estava revogado.
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"nao foi possivel revogar o aparelho {deviceId} no banco", ex);
                return false;
            }
        }

        private static bool ApagarPasta(ConnectionSnapshot.PeerRow peer)
        {
            try
            {
                if (string.IsNullOrEmpty(peer.Pasta) || !Directory.Exists(peer.Pasta)) return true;

                // Confere que a pasta esta mesmo dentro do diretorio de clientes antes
                // de apagar recursivamente.
                var raiz = Path.GetFullPath(TunnelManager.ClientsDir);
                var alvo = Path.GetFullPath(peer.Pasta);
                if (!alvo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ||
                    alvo.Length <= raiz.Length)
                {
                    Log.Error($"recusando apagar {alvo}: fora de {raiz}");
                    return false;
                }

                Directory.Delete(alvo, true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"nao foi possivel apagar a pasta de {peer.Name}", ex);
                return false;
            }
        }
    }
}
