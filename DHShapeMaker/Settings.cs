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
            get => GetRegValue("RecentProjects", string.Empty);
            set => SetRegValue("RecentProjects", value);
        }

        internal static string ProjectFolder
        {
            get => GetRegValue("ProjectDir", documentsPath);
            set => SetRegValue("ProjectDir", value);
        }

        internal static string ShapeFolder
        {
            get => GetRegValue("PdnShapeDir", documentsPath);
            set => SetRegValue("PdnShapeDir", value);
        }

        private static string GetRegValue(string valueName, string defaultValue)
        {
            return (string)regKey.GetValue(valueName, defaultValue);
        }

        private static void SetRegValue(string valueName, string value)
        {
            regKey.SetValue(valueName, value, RegistryValueKind.String);
            regKey.Flush();
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
