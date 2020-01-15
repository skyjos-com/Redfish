using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace RedfishService
{
    public class CommonUtils
    {

        public static string GetAppFolderPath()
        {
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Redfish";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return folderPath;
        }

        public static bool IsEmptyString(string value)
        {
            if (value == null)
            {
                return true;
            } 
            else if (value.Equals("")) 
            {
                return true;
            } 
            else
            {
                return false;
            }
        }
    }
}
