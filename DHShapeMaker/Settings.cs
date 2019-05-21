using Microsoft.Win32;
using System;

namespace ShapeMaker
{
    internal static class Settings
    {
        private static readonly RegistryKey regKey;
        private static readonly string documentsPath;

        internal static string RecentProjects
        {
            get
            {
                return (string)regKey.GetValue("RecentProjects", string.Empty);
            }
            set
            {
                regKey.SetValue("RecentProjects", value, RegistryValueKind.String);
                regKey.Flush();
            }
        }

        internal static string ProjectFolder
        {
            get
            {
                return (string)regKey.GetValue("ProjectDir", documentsPath);
            }
            set
            {
                regKey.SetValue("ProjectDir", value, RegistryValueKind.String);
                regKey.Flush();
            }
        }

        internal static string ShapeFolder
        {
            get
            {
                return (string)regKey.GetValue("PdnShapeDir", documentsPath);
            }
            set
            {
                regKey.SetValue("PdnShapeDir", value, RegistryValueKind.String);
                regKey.Flush();
            }
        }

        static Settings()
        {
            regKey = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true);
            if (regKey == null)
            {
                Registry.CurrentUser.CreateSubKey(@"Software\PdnDwarves\ShapeMaker").Flush();
                regKey = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true);
            }

            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
