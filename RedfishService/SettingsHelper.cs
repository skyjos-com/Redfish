/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace RedfishService
{
    public class SettingsHelper
    {
        public const string SettingsFileName = "Settings.xml";
        public const int DefaultPort = 20445;

        public static XmlDocument ReadSettingsXML()
        {
            string settingsFilePath = GetSettingsFilePath();
            XmlDocument doc = new XmlDocument();
            doc.Load(settingsFilePath);
            return doc;
        }


        public static UserCollection ReadUserSettings()
        {
            UserCollection users = new UserCollection();
            XmlDocument document = ReadSettingsXML();
            XmlNode usersNode = document.SelectSingleNode("Settings/Users");
            if (usersNode != null)
            {
                foreach (XmlNode userNode in usersNode.ChildNodes)
                {
                    string accountName = userNode.Attributes["AccountName"].Value;
                    string password = userNode.Attributes["Password"].Value;
                    users.Add(accountName, password);
                }
            }
            return users;
        }

        public static void WriteUserSettings(UserCollection users)
        {
            XmlDocument document = ReadSettingsXML();
            XmlNode usersNode = document.SelectSingleNode("Settings/Users");
            if (usersNode == null)
            {
                XmlNode settingsNode = document.SelectSingleNode("Settings");
                XmlElement aNode = document.CreateElement("Users");
                settingsNode.AppendChild(aNode);
                usersNode = aNode;
            }
            else
            {
                usersNode.RemoveAll();
            }
            

            foreach (User user in users)
            {
                XmlElement aNode = document.CreateElement("User");
                aNode.SetAttribute("AccountName", user.AccountName);
                aNode.SetAttribute("Password", user.Password);
                usersNode.AppendChild(aNode);
            }

            string settingsFilePath = GetSettingsFilePath();
            document.Save(settingsFilePath);
        }

        public static int ReadServerPort()
        {
            int port = DefaultPort;
            XmlDocument document = ReadSettingsXML();
            XmlNode portNode = document.SelectSingleNode("Settings/Port");
            if (portNode != null)
            {
                string portString = portNode.Attributes["Number"].Value;
                try
                {
                    port = int.Parse(portString);
                }
                catch
                { }
            }
            
            return port;
        }

        public static void WriteServerPort(int port)
        {
            XmlDocument document = ReadSettingsXML();
            XmlNode portNode = document.SelectSingleNode("Settings/Port");
            if (portNode == null)
            {
                XmlNode settingsNode = document.SelectSingleNode("Settings");
                XmlElement aNode = document.CreateElement("Port");
                aNode.SetAttribute("Number", port.ToString());
                settingsNode.AppendChild(aNode);
            }
            else
            {
                portNode.Attributes["Number"].Value = port.ToString();
            }
            

            string settingsFilePath = GetSettingsFilePath();
            document.Save(settingsFilePath);
        }

        public static List<ShareSettings> ReadSharesSettings()
        {
            List<ShareSettings> shares = new List<ShareSettings>();
            XmlDocument document = ReadSettingsXML();
            XmlNode sharesNode = document.SelectSingleNode("Settings/Shares");

            if (sharesNode != null)
            {
                foreach (XmlNode shareNode in sharesNode.ChildNodes)
                {
                    string shareName = shareNode.Attributes["Name"].Value;
                    string sharePath = shareNode.Attributes["Path"].Value;

                    XmlNode readAccessNode = shareNode.SelectSingleNode("ReadAccess");
                    List<string> readAccess = ReadAccessList(readAccessNode);
                    XmlNode writeAccessNode = shareNode.SelectSingleNode("WriteAccess");
                    List<string> writeAccess = ReadAccessList(writeAccessNode);
                    ShareSettings share = new ShareSettings(shareName, sharePath, readAccess, writeAccess);
                    shares.Add(share);
                }
            }

            return shares;
        }

        public static void WriteSharesSettings(List<ShareSettings> shares)
        {
            XmlDocument document = ReadSettingsXML();
            XmlNode sharesNode = document.SelectSingleNode("Settings/Shares");
            if (sharesNode == null)
            {
                XmlNode settingsNode = document.SelectSingleNode("Settings");
                sharesNode = document.CreateElement("Shares");
                settingsNode.AppendChild(sharesNode);
            } 
            else
            {
                sharesNode.RemoveAll();
            }

            foreach (ShareSettings share in shares)
            {
                XmlElement sharedNode = document.CreateElement("Shared");
                sharedNode.SetAttribute("Name", share.ShareName);
                sharedNode.SetAttribute("Path", share.SharePath);

                StringBuilder readAccounts = new StringBuilder();
                int i = 0;
                foreach(string account in share.ReadAccess)
                {
                    if (i > 0)
                    {
                        readAccounts.Append(",");
                    }
                    readAccounts.Append(account);
                    i += 1;
                }
                XmlElement readAccessNode = document.CreateElement("ReadAccess");
                readAccessNode.SetAttribute("Accounts", readAccounts.ToString());
                sharedNode.AppendChild(readAccessNode);

                StringBuilder writeAccounts = new StringBuilder();
                i = 0;
                foreach (string account in share.ReadAccess)
                {
                    if (i > 0)
                    {
                        writeAccounts.Append(",");
                    }
                    writeAccounts.Append(account);
                    i += 1;
                }
                XmlElement writeAccessNode = document.CreateElement("WriteAccess");
                writeAccessNode.SetAttribute("Accounts", writeAccounts.ToString());
                sharedNode.AppendChild(writeAccessNode);

                sharesNode.AppendChild(sharedNode);
            }

            string settingsFilePath = GetSettingsFilePath();
            document.Save(settingsFilePath);
        }

        public static bool ReadRunAsService()
        {
            bool shouldRunAsService = false;
            XmlDocument document = ReadSettingsXML();
            XmlNode serviceNode = document.SelectSingleNode("Settings/Service");
            if (serviceNode != null)
            {
                string theValue = serviceNode.Attributes["RunAsService"].Value;

                try
                {
                    shouldRunAsService = bool.Parse(theValue);
                }
                catch
                { }
            }
            
            return shouldRunAsService;
        }

        public static void WriteRunAsService(bool shouldRunAsService)
        {
            XmlDocument document = ReadSettingsXML();
            XmlNode serviceNode = document.SelectSingleNode("Settings/Service");
            if (serviceNode == null)
            {
                XmlNode settingsNode = document.SelectSingleNode("Settings");
                XmlElement aNode = document.CreateElement("Service");
                aNode.SetAttribute("RunAsService", shouldRunAsService.ToString());
                settingsNode.AppendChild(aNode);
            } 
            else
            {
                serviceNode.Attributes["RunAsService"].Value = shouldRunAsService.ToString();
            }

            string settingsFilePath = GetSettingsFilePath();
            document.Save(settingsFilePath);
        }

        private static List<string> ReadAccessList(XmlNode node)
        {
            List<string> result = new List<string>();
            if (node != null)
            {
                string accounts = node.Attributes["Accounts"].Value;
                if (accounts == "*")
                {
                    result.Add("Users");
                }
                else
                {
                    string[] splitted = accounts.Split(',');
                    result.AddRange(splitted);
                }
            }
            return result;
        }

        private static string GetSettingsFilePath()
        {
            string filePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + SettingsFileName;
            return filePath;
        }
    }
}
