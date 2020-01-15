using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.IO;
using SMBLibrary;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Authentication.NTLM;
using SMBLibrary.Server;
using SMBLibrary.Win32;
using SMBLibrary.Win32.Security;
using Utilities;
using System.Security;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;
using System.Collections;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using Redfish.About;
using RedfishService;

namespace Redfish
{
    public partial class MainWindow : Window
    {
        private SMBLibrary.Server.SMBServer m_server;
        private SMBLibrary.Server.NameServer m_nameServer;
        private LogWriter m_logWriter;
        private List<ShareSettings> m_sharesSettings;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            List<IPAddress> localIPs = NetworkInterfaceHelper.GetHostIPAddresses();
            KeyValuePairList<string, IPAddress> list = new KeyValuePairList<string, IPAddress>();
            foreach (IPAddress address in localIPs)
            {
                if (address.ToString().Equals("127.0.0.1"))
                {
                    continue;
                }
                list.Add(address.ToString(), address);
            }
            list.Add("Any", IPAddress.Any);
            this.address_combobox.ItemsSource = list;
            this.address_combobox.DisplayMemberPath = "Key";
            this.address_combobox.SelectedIndex = 0;


            UserCollection users = this.GetUserCollection();
            if (users != null)
            {
                List<string> userNames = users.ListUsers();
                if (userNames.Count > 0)
                {
                    string username = userNames[0];
                    this.username_textbox.Text = username;
                    this.password_box.Password = users.GetUserPassword(username);
                }
            }

            m_sharesSettings = this.GetShareSettings();
            this.shares_listbox.ItemsSource = m_sharesSettings;
            this.shares_listbox.DisplayMemberPath = "SharePath";

            int port = SettingsHelper.ReadServerPort();
            this.port_textbox.Text = port.ToString();

            bool shouldRunAsService = SettingsHelper.ReadRunAsService();
            this.service_checkbox.IsChecked = shouldRunAsService;
        }


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            string accountName = this.username_textbox.Text;
            string password = this.password_box.Password;
            if (CommonUtils.IsEmptyString(accountName))
            {
                MessageBox.Show("User Name or Password cannot be empty!", "Error");
                return;
            }

            List<ShareSettings> sharesSettings = this.GetShareSettings();
            if(sharesSettings == null || sharesSettings.Count == 0)
            {
                MessageBox.Show("Please add directories for sharing!", "Error");
                return;
            }

            int port = SettingsHelper.DefaultPort;
            try
            {
                port = int.Parse(this.port_textbox.Text);
                SettingsHelper.WriteServerPort(port);
            } 
            catch
            {
                MessageBox.Show("Invalid port number!", "Error");
                return;
            }

            UserCollection users = new UserCollection();
            users.Add(new User(accountName, password));
            // Save account if necessary
            if (this.NeedUpdateUserCollection())
            {
                SettingsHelper.WriteUserSettings(users);
            }

            bool runAsService = this.service_checkbox.IsChecked ?? false;
            if (runAsService)
            {
                if (!this.IsInAdminRole())
                {
                    MessageBox.Show("To start the service, please run application as administrator.", "Info");
                    return;
                }

                try
                {
                    ServiceController serviceController = new ServiceController("RedfishService");
                    serviceController.Start();
                    this.start_button.IsEnabled = false;
                    this.stop_button.IsEnabled = true;
                } 
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
                
            }
            else
            {
                KeyValuePair<string, IPAddress> selectedValue = (KeyValuePair<string, IPAddress>)this.address_combobox.SelectedValue;
                IPAddress serverAddress = selectedValue.Value;
                SMBTransportType transportType = SMBTransportType.DirectTCPTransport;

                NTLMAuthenticationProviderBase authenticationMechanism = new IndependentNTLMAuthenticationProvider(users.GetUserPassword);

                SMBShareCollection shares = new SMBShareCollection();
                foreach (ShareSettings shareSettings in sharesSettings)
                {
                    FileSystemShare share = shareSettings.InitializeShare();
                    shares.Add(share);
                }

                GSSProvider securityProvider = new GSSProvider(authenticationMechanism);
                m_server = new SMBLibrary.Server.SMBServer(shares, securityProvider);
                m_logWriter = new LogWriter();
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

                    this.start_button.IsEnabled = false;
                    this.stop_button.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }

        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            bool runAsService = this.service_checkbox.IsChecked ?? false;
            if (runAsService)
            {
                if (!this.IsInAdminRole())
                {
                    MessageBox.Show("To stop the service, please run application as administrator.", "Info");
                    return;
                }

                try
                {
                    ServiceController serviceController = new ServiceController("RedfishService");
                    if (serviceController.CanStop)
                    {
                        serviceController.Stop();
                        this.start_button.IsEnabled = true;
                        this.stop_button.IsEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
            else
            {
                if (m_server != null)
                {
                    m_server.Stop();
                    m_logWriter.CloseLogFile();
                    this.start_button.IsEnabled = true;
                    this.stop_button.IsEnabled = false;

                    if (m_nameServer != null)
                    {
                        m_nameServer.Stop();
                    }
                }
            }
        }


        private void AddShareButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbDialog = new FolderBrowserDialog();
            DialogResult result = fbDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string folderPath = fbDialog.SelectedPath;
                string folderName = System.IO.Path.GetFileName(folderPath);
                if (CommonUtils.IsEmptyString(folderName))
                {
                    folderName = folderPath.Substring(0,1);
                }

                List<string> permissionList = new List<string>();
                permissionList.Add("*");
                ShareSettings share = new ShareSettings(folderName, folderPath, permissionList, permissionList);
                this.m_sharesSettings.Add(share);
                this.shares_listbox.Items.Refresh();

                SettingsHelper.WriteSharesSettings(m_sharesSettings);
            }
        }

        private void RemoveShareButton_Click(object sender, RoutedEventArgs e)
        {
            IList selectedItems = this.shares_listbox.SelectedItems;
            if (selectedItems.Count == 0)
            {
                return;
            }

            foreach (ShareSettings share in selectedItems)
            {
                this.m_sharesSettings.Remove(share);
            }
            this.shares_listbox.Items.Refresh();
            SettingsHelper.WriteSharesSettings(this.m_sharesSettings);
        }

        private void ServiceCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsInAdminRole())
            {
                string fileName;
                bool runAsService = this.service_checkbox.IsChecked ?? false;
                if (runAsService)
                {
                    fileName = "InstallService.bat"; 
                } 
                else
                {
                    fileName = "UninstallService.bat";
                }

                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = fileName;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                SettingsHelper.WriteRunAsService(runAsService);
            }
            else
            {
                bool shouldRunAsService = SettingsHelper.ReadRunAsService();
                this.service_checkbox.IsChecked = shouldRunAsService;
                MessageBox.Show("To start the service, please run application as administrator.", "Info");
            }
        }

        private bool IsInAdminRole()
        {
            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return isElevated;
        }

        private bool NeedUpdateUserCollection()
        {
            bool needUpdate = false;
            UserCollection oldUsers = this.GetUserCollection();
            if (oldUsers != null && oldUsers.Count > 0)
            {
                string accountName = this.username_textbox.Text;
                string password = this.password_box.Password;
                User oldUser = oldUsers[0];
                if (!oldUser.AccountName.Equals(accountName) || !oldUser.Password.Equals(password))
                {
                    needUpdate = true;
                }
            } 
            else
            {
                needUpdate = true;
            }

            return needUpdate;
        }

        private UserCollection GetUserCollection()
        {
            UserCollection users = null;
            try
            {
                users = SettingsHelper.ReadUserSettings();
            }
            catch (Exception)
            {
            }
            return users;
        }

        private List<ShareSettings> GetShareSettings()
        {
            List<ShareSettings> sharesSettings = null;
            try
            {
                sharesSettings = SettingsHelper.ReadSharesSettings();
            }
            catch (Exception)
            {
            }

            return sharesSettings;
        }

        private void InputNumbers(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void AboutButton_click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Show();
        }
    }
}
