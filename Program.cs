namespace ProcessHibernator {
    internal static class Program {
        private static Mutex? _mutex;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            const string appName = "Global\\ProcessHibernator_SingleInstance_Mutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew) {
                MessageBox.Show("Another instance of Process Hibernator is already running.", "Instance Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());

            // Keep the mutex alive until the app closes
            GC.KeepAlive(_mutex);
        }
    }
}