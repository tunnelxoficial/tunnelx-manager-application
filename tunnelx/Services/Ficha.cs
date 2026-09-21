using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace tunnelx.Services
{
    /// <summary>
    /// A ficha de um cliente no disco: plano, titular e prazo de acesso.
    /// </summary>
    /// <remarks>
    /// Mora num arquivo IRMAO do client.json — <c>meta.json</c>, na mesma pasta —
    /// e nunca dentro dele. O client.json e relido por
    /// <see cref="TunnelManager.WriteServerConfFromClients"/> para montar o
    /// TunnelX.conf: acrescentar campos ali coloca a montagem do tunel em risco
    /// por uma informacao que e so de tela. A varredura daquele metodo itera
    /// DIRETORIOS e abre "client.json" pelo nome, entao um arquivo ao lado e
    /// invisivel para ela — as pastas de cliente ja convivem com um
    /// client-peer.png pelo mesmo motivo.
    ///
    /// A ficha e um CACHE da consulta ao banco. A grade se atualiza de segundo em
    /// segundo e o banco fica num servidor remoto; consultar ali seria trocar o
    /// travamento que acabamos de tirar por outro. Quem escreve e o ciclo de 60
    /// segundos do provisionador.
    ///
    /// Por isso a ficha guarda so o que NAO envelhece: nome do plano, nome do
    /// titular, papel, rotulo do prazo e a data absoluta de expiracao. A contagem
    /// regressiva ("expira em 29 dias") e calculada na hora de desenhar — gravada
    /// em disco ela mentiria para o operador no dia seguinte.
    /// </remarks>
    public class Ficha
    {
        public const string NomeArquivo = "meta.json";

        /// <summary>Nome do plano contratado. Nulo para cliente fora do banco.</summary>
        public string Plano;

        /// <summary>Nome do titular que paga pelo tunel.</summary>
        public string Dono;

        /// <summary>"titular" ou "convidado".</summary>
        public string Papel;

        /// <summary>Rotulo do prazo como o servidor ja o escreve: "30 dias", "Sem prazo".</summary>
        public string Prazo;

        /// <summary>Quando o acesso do convidado acaba. Data ABSOLUTA, em ISO-8601.</summary>
        public string Expira;

        /// <summary>Vagas contratadas nesta conexao.</summary>
        public int Vagas;

        /// <summary>
        /// A conexao (o tunel) a que este cliente pertence.
        /// </summary>
        /// <remarks>
        /// Guardado no lugar de um contador de ocupacao. O contador mudaria para
        /// todos os aparelhos da conexao sempre que um entrasse ou saisse, e cada
        /// mudanca e uma regravacao de arquivo debaixo de um leitor de 1 segundo.
        /// O id nao muda nunca, e contar quantas linhas o compartilham e trivial
        /// na hora de montar a tela.
        /// </remarks>
        public int Conexao;

        public bool Convidado
        {
            get { return string.Equals(Papel, "convidado", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Quanto falta para o acesso acabar, calculado agora.
        /// </summary>
        /// <returns>Nulo quando nao ha prazo ou a data nao pode ser lida.</returns>
        public TimeSpan? Restante(DateTime agora)
        {
            if (string.IsNullOrWhiteSpace(Expira)) return null;

            DateTime quando;
            if (!DateTime.TryParse(Expira, System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.AdjustToUniversal |
                                   System.Globalization.DateTimeStyles.AssumeUniversal, out quando))
                return null;

            return quando - agora.ToUniversalTime();
        }

        /// <summary>
        /// O prazo do convidado ja passou?
        /// </summary>
        /// <remarks>
        /// Decidido pela DATA e nao pelo status do convite. No servidor, a varredura
        /// que marca convites como EXPIRED e preguicosa: so roda quando alguem
        /// consulta a ocupacao daquele tunel. Enquanto nenhum aparelho daquela
        /// conexao fizer isso, o convite segue ACTIVE com a data no passado — e a
        /// tela afirmaria acesso valido para quem ja perdeu o prazo.
        /// </remarks>
        public bool Vencido(DateTime agora)
        {
            var falta = Restante(agora);
            return falta.HasValue && falta.Value.TotalSeconds <= 0;
        }

        /* ----------------------------------------------------------- leitura -- */

        /// <summary>
        /// Le a ficha da pasta de um cliente. Devolve null quando nao ha ficha —
        /// que e o caso NORMAL de um cliente criado a mao pela tela.
        /// </summary>
        public static Ficha Ler(string pastaDoCliente)
        {
            try
            {
                var caminho = Path.Combine(pastaDoCliente, NomeArquivo);
                if (!File.Exists(caminho)) return null;

                return DeJson(File.ReadAllText(caminho));
            }
            catch
            {
                // Arquivo sendo trocado pelo provisionador, ou conteudo quebrado.
                // Ficar sem ficha e melhor que derrubar a coleta inteira.
                return null;
            }
        }

        public static Ficha DeJson(string texto)
        {
            var campos = Json.LerObjeto(texto);
            if (campos == null || campos.Count == 0) return null;

            var f = new Ficha
            {
                Plano = Pegar(campos, "plano"),
                Dono = Pegar(campos, "dono"),
                Papel = Pegar(campos, "papel"),
                Prazo = Pegar(campos, "prazo"),
                Expira = Pegar(campos, "expira")
            };

            int n;
            if (int.TryParse(Pegar(campos, "vagas"), out n)) f.Vagas = n;
            if (int.TryParse(Pegar(campos, "conexao"), out n)) f.Conexao = n;

            return f;
        }

        private static string Pegar(Dictionary<string, string> d, string chave)
        {
            string v;
            return d.TryGetValue(chave, out v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        }

        /* ------------------------------------------------------------ escrita -- */

        public string ParaJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"plano\": \"").Append(Json.Escapar(Plano)).Append("\",\n");
            sb.Append("  \"dono\": \"").Append(Json.Escapar(Dono)).Append("\",\n");
            sb.Append("  \"papel\": \"").Append(Json.Escapar(Papel)).Append("\",\n");
            sb.Append("  \"prazo\": \"").Append(Json.Escapar(Prazo)).Append("\",\n");
            sb.Append("  \"expira\": \"").Append(Json.Escapar(Expira)).Append("\",\n");
            sb.Append("  \"vagas\": ").Append(Vagas).Append(",\n");
            sb.Append("  \"conexao\": ").Append(Conexao).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Grava a ficha, e devolve true se algo mudou no disco.
        /// </summary>
        /// <remarks>
        /// Duas precaucoes, ambas por causa de quem le do outro lado:
        ///
        /// 1. So grava quando o conteudo mudou. A grade percorre estas pastas de
        ///    segundo em segundo; reescrever N arquivos por minuto sem necessidade
        ///    e trabalho de disco puro e mais chance de pegar arquivo pela metade.
        ///
        /// 2. Grava em .tmp e troca. O Program.cs nao tem Mutex — nada impede duas
        ///    copias do provisionador abertas, e este passo fica fora do claim
        ///    atomico do banco. File.WriteAllText direto deixaria o leitor pegar
        ///    meio arquivo; a troca no NTFS e atomica.
        /// </remarks>
        public static bool Gravar(string pastaDoCliente, Ficha ficha)
        {
            if (ficha == null) return false;

            try
            {
                if (!Directory.Exists(pastaDoCliente)) Directory.CreateDirectory(pastaDoCliente);

                var caminho = Path.Combine(pastaDoCliente, NomeArquivo);
                var novo = ficha.ParaJson();

                if (File.Exists(caminho))
                {
                    string atual = null;
                    try { atual = File.ReadAllText(caminho); } catch { }
                    if (string.Equals(atual, novo, StringComparison.Ordinal)) return false;
                }

                var temporario = caminho + ".tmp";
                File.WriteAllText(temporario, novo);

                if (File.Exists(caminho)) File.Replace(temporario, caminho, null);
                else File.Move(temporario, caminho);

                return true;
            }
            catch (Exception ex)
            {
                Log.Aviso($"nao foi possivel gravar a ficha em {pastaDoCliente}: {ex.Message}");
                return false;
            }
        }
    }
}
