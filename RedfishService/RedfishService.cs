using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using SMBLibrary;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Authentication.NTLM;
using SMBLibrary.Server;
using SMBLibrary.Win32;
using SMBLibrary.Win32.Security;
using Utilities;

namespace RedfishService
{
    public partial class RedfishService : ServiceBase
    {
        private LogWriter m_logWriter = new LogWriter();
        private SMBLibrary.Server.SMBServer m_server;
        private SMBLibrary.Server.NameServer m_nameServer;

        public RedfishService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            this.StartService();
        }

        protected override void OnStop()
        {
            this.StopService();
        }

        private void StartService()
        {
            IPAddress serverAddress = IPAddress.Any;
            SMBTransportType transportType = SMBTransportType.DirectTCPTransport;

            UserCollection users = null;
            List<ShareSettings> sharesSettings = null;
            int port = SettingsHelper.DefaultPort;
            try
            {
                users = SettingsHelper.ReadUserSettings();
                sharesSettings = SettingsHelper.ReadSharesSettings();
                port = SettingsHelper.ReadServerPort();
            }
            catch
            {
                m_logWriter.WriteLine("Fail to load Settings.xml");
                return;
            }

            NTLMAuthenticationProviderBase authenticationMechanism = new IndependentNTLMAuthenticationProvider(users.GetUserPassword);

            SMBShareCollection shares = new SMBShareCollection();
            foreach (ShareSettings shareSettings in sharesSettings)
            {
                FileSystemShare share = shareSettings.InitializeShare();
                shares.Add(share);
            }

            GSSProvider securityProvider = new GSSProvider(authenticationMechanism);
            m_server = new SMBLibrary.Server.SMBServer(shares, securityProvider);
            // The provided logging mechanism will synchronously write to the disk during server activity.
            // To maximize server performance, you can disable logging by commenting out the following line.
            m_server.LogEntryAdded += new EventHandler<LogEntry>(m_logWriter.OnLogEntryAdded);

            try
            {
                m_server.Start(serverAddress, port);
                if (transportType == SMBTransportType.NetBiosOverTCP)
                {
                    if (serverAddress.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Equals(serverAddress, IPAddress.Any))
                    {
                        IPAddress subnetMask = NetworkInterfaceHelper.GetSubnetMask(serverAddress);
                        m_nameServer = new NameServer(serverAddress, subnetMask);
                        m_nameServer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                m_logWriter.WriteLine(ex.Message);
            }
        }


        public void StopService()
        {
            if (m_server != null)
            {
                m_server.Stop();
                m_logWriter.CloseLogFile();

                if (m_nameServer != null)
                {
                    m_nameServer.Stop();
                }
            }
        }

    }
}
