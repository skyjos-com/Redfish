using System;
using System.ServiceProcess;
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

            if (args.Length == 1 && (args[0] == "--install-service" || args[0] == "--uninstall-service"))
            {
                try
                {
                    if (args[0] == "--install-service")
                        WindowsServiceManager.Install();
                    else
                        WindowsServiceManager.Uninstall();
                    return 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Redfish service", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
            }

            if (args.Length != 0)
            {
                return 2;
            }

            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
