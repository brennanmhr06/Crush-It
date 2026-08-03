using System;
using System.Windows.Forms;
using CrushIt.UI;

namespace CrushIt
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            Application.Run(new LoadingForm());
        }
    }
}

