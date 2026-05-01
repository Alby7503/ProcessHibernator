using System;
using System.Threading;
using System.Windows.Forms;

namespace ProcessHibernator {
    internal static class Program {
        private static Mutex? _mutex;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // Ensure single instance
            _mutex = new Mutex(true, "Global\\ProcessHibernator_SingleInstance", out bool createdNew);
            if (!createdNew) {
                MessageBox.Show("Process Hibernator is already running.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            // Keep the mutex alive until the app closes
            GC.KeepAlive(_mutex);
        }
    }
}
