using System;
using System.Data.SqlClient;

namespace tunnelx.Services
{
    /// <summary>
    /// Grava no banco a decisao de cortar ou devolver a internet de uma conexao.
    /// </summary>
    /// <remarks>
    /// Existe porque a fonte da verdade mudou de lugar.
    ///
    /// O menu "Desativar conexao" da grade cortava o cliente so no tunel e no
    /// disco. Isso bastava enquanto ninguem comparava as duas pontas. Agora o
    /// provisionador reconcilia o tunel CONTRA o banco a cada ciclo: um corte que
    /// nao chega ao banco e lido como divergencia e DESFEITO em ate ~60 s — o
    /// operador cortava o cliente, via o peer sumir, e um minuto depois ele
    /// estava de volta, sem nenhuma mensagem explicando.
    ///
    /// Por isso a escrita aqui nao pode falhar em silencio: quem chama precisa
    /// saber para poder avisar o operador de que o corte NAO vai durar.
    ///
    /// O motivo gravado e sempre 'operator'. E a mesma convencao do painel
    /// (backend/services/acessoInternet.js): corte do operador so a mao do
    /// operador desfaz, enquanto corte por inadimplencia o pagamento desfaz
    /// sozinho. Se este caminho gravasse 'overdue', o primeiro pagamento
    /// confirmado devolveria a internet a quem foi cortado de proposito.
    /// </remarks>
    public static class Acesso
    {
        private const string MotivoOperador = "operator";

        /// <summary>
        /// Escreve o estado desejado de acesso da conexao.
        /// </summary>
        /// <param name="conexaoId">Id em Connections. Zero ou negativo: nao ha o que gravar.</param>
        /// <param name="liberada">true devolve a internet, false corta.</param>
        /// <returns>
        /// true quando a decisao esta gravada (ou quando nao havia conexao no
        /// banco para gravar, caso do cliente avulso so em disco).
        /// </returns>
        public static bool Definir(int conexaoId, bool liberada)
        {
            // Cliente avulso, que existe so em disco: nao ha linha em Connections,
            // e a reconciliacao tambem nao o enxerga — o corte no disco basta.
            if (conexaoId <= 0) return true;

            var conexaoBanco = DbBackgroundService.StringDeConexao;
            if (string.IsNullOrWhiteSpace(conexaoBanco))
            {
                Log.Error($"conexao {conexaoId}: banco nao configurado, o corte NAO foi gravado");
                return false;
            }

            var sql = liberada
                ? @"UPDATE Connections
                       SET internet = 1,
                           internet_block_reason = NULL,
                           internet_blocked_at = NULL
                     WHERE id = @id"
                : @"UPDATE Connections
                       SET internet = 0,
                           internet_block_reason = @motivo,
                           internet_blocked_at = ISNULL(internet_blocked_at, SYSDATETIMEOFFSET())
                     WHERE id = @id";

            try
            {
                using (var conn = new SqlConnection(conexaoBanco))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", conexaoId);
                        if (!liberada) cmd.Parameters.AddWithValue("@motivo", MotivoOperador);

                        var linhas = cmd.ExecuteNonQuery();

                        if (linhas == 0)
                        {
                            Log.Aviso($"conexao {conexaoId}: nao existe no banco, nada gravado");
                            return true;   // nao ha o que reconciliar contra
                        }
                    }
                }

                Log.Info($"conexao {conexaoId}: acesso {(liberada ? "LIBERADO" : "CORTADO")} pelo operador");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"conexao {conexaoId}: falha ao gravar o acesso no banco", ex);
                return false;
            }
        }
    }
}
