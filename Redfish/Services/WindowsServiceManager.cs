using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32.SafeHandles;

namespace Redfish
{
    internal static class WindowsServiceManager
    {
        internal const string ServiceName = "RedfishService";
        private const string DisplayName = "Redfish SMB Service";
        private const uint ScManagerConnect = 0x0001;
        private const uint ScManagerCreateService = 0x0002;
        private const uint ServiceChangeConfig = 0x0002;
        private const uint DeleteAccess = 0x00010000;
        private const uint ServiceWin32OwnProcess = 0x0010;
        private const uint ServiceAutoStart = 2;
        private const uint ServiceErrorNormal = 1;
        private const uint ServiceNoChange = 0xFFFFFFFF;
        private const int ErrorServiceDoesNotExist = 1060;

        internal static void Install()
        {
            string executablePath = Assembly.GetExecutingAssembly().Location;
            string command = "\"" + executablePath + "\" --service";
            using (var manager = OpenSCManager(null, null, ScManagerConnect | ScManagerCreateService))
            {
                CheckHandle(manager);
                var service = OpenService(manager, ServiceName, ServiceChangeConfig | DeleteAccess);
                bool created = false;
                if (service.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    service.Dispose();
                    if (error != ErrorServiceDoesNotExist)
                        throw new Win32Exception(error);
                    service = CreateService(manager, ServiceName, DisplayName,
                        ServiceChangeConfig | DeleteAccess, ServiceWin32OwnProcess,
                        ServiceAutoStart, ServiceErrorNormal, command, null, IntPtr.Zero, null, null, null);
                    CheckHandle(service);
                    created = true;
                }

                using (service)
                {
                    try
                    {
                        if (!created && !ChangeServiceConfig(service, ServiceNoChange, ServiceAutoStart,
                            ServiceNoChange, command, null, IntPtr.Zero, null, null, null, DisplayName))
                            throw new Win32Exception(Marshal.GetLastWin32Error());

                        var description = new ServiceDescription { Description = "Redfish SMB file sharing service" };
                        if (!ChangeServiceConfig2(service, 1, ref description))
                            throw new Win32Exception(Marshal.GetLastWin32Error());

                        UpdateFirewall(executablePath);
                    }
                    catch
                    {
                        // Undo a newly created service if firewall setup fails.
                        if (created)
                            DeleteService(service);
                        throw;
                    }
                }
            }
        }

        internal static void Uninstall()
        {
            using (var manager = OpenSCManager(null, null, ScManagerConnect))
            {
                CheckHandle(manager);
                using (var service = OpenService(manager, ServiceName, DeleteAccess))
                {
                    if (service.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ErrorServiceDoesNotExist)
                            throw new Win32Exception(error);
                    }
                    else
                    {
                        using (var controller = new ServiceController(ServiceName))
                        {
                            if (controller.Status != ServiceControllerStatus.Stopped)
                            {
                                if (controller.Status != ServiceControllerStatus.StopPending)
                                    controller.Stop();
                                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                            }
                        }
                        if (!DeleteService(service))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }
            UpdateFirewall(null);
        }

        private static void UpdateFirewall(string executablePath)
        {
            object policyObject = null;
            object rulesObject = null;
            object ruleObject = null;
            try
            {
                policyObject = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", true));
                dynamic policy = policyObject;
                rulesObject = policy.Rules;
                dynamic rules = rulesObject;
                if (executablePath == null)
                {
                    rules.Remove(ServiceName);
                    return;
                }

                ruleObject = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule", true));
                dynamic rule = ruleObject;
                rule.Name = ServiceName;
                rule.Description = "Allow incoming Redfish SMB connections";
                rule.ApplicationName = executablePath;
                rule.Protocol = 6; // TCP
                rule.Direction = 1; // Inbound
                rule.Action = 1; // Allow
                rule.Profiles = int.MaxValue; // Preserve the previous rule's profile coverage.
                rule.Enabled = true;
                rules.Add(rule);
            }
            finally
            {
                if (ruleObject != null) Marshal.FinalReleaseComObject(ruleObject);
                if (rulesObject != null) Marshal.FinalReleaseComObject(rulesObject);
                if (policyObject != null) Marshal.FinalReleaseComObject(policyObject);
            }
        }

        private static void CheckHandle(ServiceHandle handle)
        {
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error);
            }
        }

        private sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public ServiceHandle() : base(true) { }
            protected override bool ReleaseHandle() { return CloseServiceHandle(handle); }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ServiceDescription
        {
            public string Description;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ServiceHandle OpenSCManager(string machineName, string databaseName, uint access);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ServiceHandle OpenService(ServiceHandle manager, string name, uint access);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ServiceHandle CreateService(ServiceHandle manager, string name, string displayName,
            uint access, uint type, uint startType, uint errorControl, string binaryPath, string loadOrderGroup,
            IntPtr tagId, string dependencies, string account, string password);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(ServiceHandle service, uint type, uint startType,
            uint errorControl, string binaryPath, string loadOrderGroup, IntPtr tagId, string dependencies,
            string account, string password, string displayName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig2(ServiceHandle service, uint infoLevel, ref ServiceDescription info);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(ServiceHandle service);

        [DllImport("advapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr handle);
    }
}
