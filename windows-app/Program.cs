using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Mause Seri Tık")]
[assembly: AssemblyDescription("Pencereye özel Windows sol tuş tekrarlayıcısı")]
[assembly: AssemblyProduct("Mause Seri Tık")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace MauseSeriTik
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool firstInstance;
            using (var instance = new Mutex(true, "Local\\MauseSeriTik", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("Mause Seri Tık zaten açık.", "Mause Seri Tık", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
