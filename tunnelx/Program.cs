using System;
using System.Windows.Forms;

namespace tunnelx
{
    internal static class Program
    {
        /// <summary>Ponto de entrada da aplicação.</summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmDefault());
        }
    }
}