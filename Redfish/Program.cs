using System;
using System.ServiceProcess;
using System.Threading;
using System.Windows;

namespace Redfish
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--service")
            {
                using (var service = new RedfishService())
                {
                    ServiceBase.Run(service);
                }
                return 0;
            }

            bool quiet = args.Length == 2 && args[1] == "--quiet";
            if ((args.Length == 1 || quiet) && IsServiceCommand(args[0]))
            {
                try
                {
                    if (args[0] == "--install-service")
                        WindowsServiceManager.Install();
                    else if (args[0] == "--uninstall-service")
                        WindowsServiceManager.Uninstall();
                    else if (args[0] == "--start-service")
                        WindowsServiceManager.Start();
                    else
                        WindowsServiceManager.Stop();
                    return 0;
                }
                catch (Exception ex)
                {
                    if (quiet)
                        Console.Error.WriteLine(ex.ToString());
                    else
                        MessageBox.Show(ex.Message, "Redfish service", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
            }

            if (args.Length != 0)
            {
                return 2;
            }

            // Inno Setup checks this mutex before replacing or removing a running UI.
            using (var uiMutex = new Mutex(false, @"Global\Skyjos.Redfish.UI"))
            {
                var app = new App();
                app.InitializeComponent();
                return app.Run();
            }
        }

        private static bool IsServiceCommand(string command)
        {
            return command == "--install-service" || command == "--uninstall-service" ||
                command == "--start-service" || command == "--stop-service";
        }
    }
}
