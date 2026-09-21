using System;
using System.Collections.Generic;
using System.Text;

namespace tunnelx.Services
{
    /// <summary>
    /// Reduz um arquivo .conf ao que o protocolo WireGuard entende.
    ///
    /// Existe por um motivo caro. O TunnelX.conf e escrito no formato do
    /// wireguard-windows, que aceita diretivas de configuracao da INTERFACE
    /// (Address, MTU, DNS, Table, PreUp...). O "wg.exe syncconf" nao conhece
    /// nenhuma delas: ele fala o protocolo, nao o wg-quick. Ao encontrar a
    /// primeira, aborta com "Line unrecognized" ANTES de tocar na interface.
    ///
    /// Na pratica isso significava que o syncconf falhava SEMPRE. E quem chamava
    /// caia no plano B — desinstalar e reinstalar o servico do tunel — que
    /// derruba todos os clientes conectados. Com o timer de status a cada
    /// segundo, o resultado era um laco: o tunel subia, era reinstalado, caia,
    /// subia de novo. Cada volta matava toda conexao TCP de todo cliente, e a
    /// internet de quem estava no tunel virava sobra entre duas quedas.
    ///
    /// O wg-quick do Linux resolve isso com "wg-quick strip". Isto e o strip.
    /// </summary>
    public static class ConfWg
    {
        // O que cada secao aceita. Qualquer outra chave e do wg-quick e sai.
        private static readonly HashSet<string> DaInterface =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "PrivateKey", "ListenPort", "FwMark" };

        private static readonly HashSet<string> DoPeer =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "PublicKey", "PresharedKey", "AllowedIPs", "Endpoint", "PersistentKeepalive" };

        /// <summary>
        /// Devolve o mesmo .conf sem as diretivas de interface do wg-quick.
        /// Preserva a ordem e a separacao das secoes: o syncconf usa a ordem
        /// para saber a qual peer cada chave pertence.
        /// </summary>
        public static string Despir(string conf)
        {
            if (string.IsNullOrEmpty(conf)) return string.Empty;

            var saida = new StringBuilder();
            HashSet<string> aceitas = null;   // null = antes de qualquer secao

            foreach (var bruta in conf.Replace("\r\n", "\n").Split('\n'))
            {
                var linha = bruta.Trim();

                // Comentario e linha vazia nao mudam nada e nao ajudam o wg.
                if (linha.Length == 0 || linha[0] == '#') continue;

                if (linha[0] == '[')
                {
                    if (linha.Equals("[Interface]", StringComparison.OrdinalIgnoreCase))
                        aceitas = DaInterface;
                    else if (linha.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                        aceitas = DoPeer;
                    else
                        aceitas = null;       // secao desconhecida: nada dela passa

                    if (aceitas != null) saida.AppendLine(linha);
                    continue;
                }

                if (aceitas == null) continue;

                var igual = linha.IndexOf('=');
                if (igual <= 0) continue;

                var chave = linha.Substring(0, igual).Trim();
                if (!aceitas.Contains(chave)) continue;

                // Normaliza o espacamento: "Chave = valor".
                saida.AppendLine(chave + " = " + linha.Substring(igual + 1).Trim());
            }

            return saida.ToString();
        }
    }
}
