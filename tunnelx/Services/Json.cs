using System.Collections.Generic;
using System.Text;

namespace tunnelx.Services
{
    /// <summary>
    /// O minimo de JSON de que o provisionador precisa: escapar valores.
    /// </summary>
    /// <remarks>
    /// Existe porque o mesmo escape precisa valer em TODOS os lugares que gravam
    /// client.json, e nao so em um. O arquivo nao e um registro qualquer: ele e
    /// relido por TunnelManager.WriteServerConfFromClients() para montar o
    /// TunnelX.conf. Um valor cru com aspas nao "quebra o arquivo" apenas — ele
    /// permite que um nome escolhido no cadastro publico injete publicKey ou
    /// AllowedIPs de terceiros no tunel.
    ///
    /// O escape ja existia dentro de DeviceProvisioner, privado. Os outros tres
    /// pontos de gravacao seguiam interpolando direto.
    /// </remarks>
    public static class Json
    {
        /// <summary>Escapa o que nao pode aparecer cru dentro de uma string JSON.</summary>
        public static string Escapar(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;

            var sb = new StringBuilder(valor.Length + 8);
            foreach (var c in valor)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Le um objeto JSON simples (um nivel, valores escalares) num dicionario.
        /// </summary>
        /// <remarks>
        /// O projeto ja tem um leitor: ConnectionSnapshot.Campo, que procura a
        /// primeira LINHA contendo a chave e corta no primeiro ':'. Ele existe
        /// porque o client.json e um formato herdado com um campo por linha, e
        /// mexer nele e arriscado. Mas ele tem dois defeitos que importam para
        /// campos vindos de cadastro de terceiros: nao desfaz escape nenhum (um
        /// plano chamado Plano "Ouro" apareceria na tela com contrabarras), e casa
        /// a chave por substring em qualquer posicao da linha — um VALOR que
        /// contenha o texto de outra chave sequestra a leitura dela.
        ///
        /// Este leitor percorre o texto de verdade: reconhece string com escape,
        /// numero, booleano e nulo, e so aceita chave em posicao de chave.
        /// Serve ao meta.json, que e arquivo novo e nao deve nada ao formato antigo.
        /// </remarks>
        public static Dictionary<string, string> LerObjeto(string texto)
        {
            var mapa = new Dictionary<string, string>(System.StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(texto)) return mapa;

            var i = 0;
            PularEspaco(texto, ref i);
            if (i >= texto.Length || texto[i] != '{') return mapa;
            i++;

            while (i < texto.Length)
            {
                PularEspaco(texto, ref i);
                if (i >= texto.Length) break;
                if (texto[i] == '}') break;
                if (texto[i] == ',') { i++; continue; }
                if (texto[i] != '"') break;   // chave tem que ser string

                var chave = LerTexto(texto, ref i);
                if (chave == null) break;

                PularEspaco(texto, ref i);
                if (i >= texto.Length || texto[i] != ':') break;
                i++;
                PularEspaco(texto, ref i);
                if (i >= texto.Length) break;

                string valor;
                if (texto[i] == '"')
                {
                    valor = LerTexto(texto, ref i);
                    if (valor == null) break;
                }
                else
                {
                    // Numero, true, false, null: vale ate a virgula ou o fecha-chaves.
                    var inicio = i;
                    while (i < texto.Length && texto[i] != ',' && texto[i] != '}') i++;
                    valor = texto.Substring(inicio, i - inicio).Trim();
                }

                mapa[chave] = valor;
            }

            return mapa;
        }

        private static void PularEspaco(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        /// <summary>Le uma string JSON a partir da aspa de abertura, desfazendo o escape.</summary>
        private static string LerTexto(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;

            var sb = new StringBuilder();
            while (i < s.Length)
            {
                var c = s[i++];

                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return null;

                var e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) return null;
                        int cod;
                        if (int.TryParse(s.Substring(i, 4),
                                         System.Globalization.NumberStyles.HexNumber,
                                         System.Globalization.CultureInfo.InvariantCulture, out cod))
                            sb.Append((char)cod);
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }

            return null;   // string sem fechamento: arquivo truncado
        }
    }
}
