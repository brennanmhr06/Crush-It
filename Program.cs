using System;
using System.Windows.Forms;
using CrushIt.UI;
using CrushIt.Core;

namespace CrushIt
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Initialize sound system
            SoundManager.Initialize();

            // Show loading form without tying application lifecycle to it
            LoadingForm loadingForm = new LoadingForm();
            loadingForm.Show();
            
            // Handle application lifecycle - exit when no forms remain
            loadingForm.FormClosed += (s, e) => {
                if (Application.OpenForms.Count == 0)
                {
                    SoundManager.Cleanup();
                    Application.Exit();
                }
            };
            
            // Run application message loop without a main form
            Application.Run();
        }
    }
}

