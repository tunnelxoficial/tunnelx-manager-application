using System;
using System.IO;
using System.Text;

namespace tunnelx.Services
{
    /// <summary>
    /// Registro em arquivo do provisionador.
    ///
    /// Existe porque nao existia nada. Os erros iam para Console.WriteLine, e um
    /// WinForms nao tem console: a mensagem era descartada. Na pratica, quando um
    /// cliente pagava e o tunel nao subia, nao havia onde olhar — nem para saber
    /// que tinha acontecido, nem para descobrir por que.
    ///
    /// Arquivo por dia, com rotacao simples por contagem de dias. Nada de
    /// biblioteca: o projeto e .NET Framework 4.8 sem injecao de dependencia, e
    /// uma dependencia nova aqui custa mais do que as ~80 linhas abaixo.
    ///
    /// Thread-safe porque o servico de fundo escreve da thread do timer enquanto
    /// a UI escreve da thread principal.
    /// </summary>
    public static class Log
    {
        private static readonly object Trava = new object();

        /// <summary>Quantos dias de log manter. O resto e apagado na abertura.</summary>
        private const int DiasParaManter = 30;

        private static string _pasta;
        private static bool _limpou;

        private static string Pasta
        {
            get
            {
                if (_pasta != null) return _pasta;
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    _pasta = Path.Combine(baseDir, "logs");
                    if (!Directory.Exists(_pasta)) Directory.CreateDirectory(_pasta);
                }
                catch
                {
                    // Sem permissao de escrita ao lado do executavel: cai para o
                    // perfil do usuario. Perder o log e pior que o local ser feio.
                    _pasta = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TunnelX", "logs");
                    try { if (!Directory.Exists(_pasta)) Directory.CreateDirectory(_pasta); }
                    catch { _pasta = null; }
                }
                return _pasta;
            }
        }

        public static void Info(string mensagem) { Escrever("INFO ", mensagem, null); }

        public static void Aviso(string mensagem) { Escrever("AVISO", mensagem, null); }

        public static void Error(string mensagem, Exception erro = null) { Escrever("ERRO ", mensagem, erro); }

        private static void Escrever(string nivel, string mensagem, Exception erro)
        {
            // O console continua recebendo: util ao depurar com o app anexado.
            Console.WriteLine("[" + nivel.Trim() + "] " + mensagem);

            var pasta = Pasta;
            if (pasta == null) return;

            try
            {
                lock (Trava)
                {
                    if (!_limpou) { LimparAntigos(pasta); _limpou = true; }

                    var linha = new StringBuilder()
                        .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                        .Append(' ').Append(nivel).Append(' ')
                        .Append(mensagem);

                    if (erro != null)
                    {
                        linha.AppendLine()
                             .Append("    ").Append(erro.GetType().Name).Append(": ").Append(erro.Message);
                        if (erro.StackTrace != null) linha.AppendLine().Append(erro.StackTrace);
                    }

                    var arquivo = Path.Combine(pasta, "tunnelx-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                    File.AppendAllText(arquivo, linha.ToString() + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Falha ao registrar nunca pode derrubar o provisionamento.
            }
        }

        private static void LimparAntigos(string pasta)
        {
            try
            {
                var limite = DateTime.Now.AddDays(-DiasParaManter);
                foreach (var arquivo in Directory.GetFiles(pasta, "tunnelx-*.log"))
                {
                    if (File.GetLastWriteTime(arquivo) < limite) File.Delete(arquivo);
                }
            }
            catch { }
        }
    }
}
