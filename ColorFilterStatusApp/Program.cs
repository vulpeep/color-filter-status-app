using System;
using System.Threading;
using System.Windows.Forms;

namespace ColorFilterStatusApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Ensure only one instance of the application is running
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "ColorFilterStatusApp_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running, exit the application
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
