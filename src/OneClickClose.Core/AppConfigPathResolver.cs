using System;
using System.IO;

namespace OneClickClose.Core
{
    public static class AppConfigPathResolver
    {
        public const string AppFolderName = "OneClickClose";
        public const string DefaultConfigFileName = "close-user-apps.config.json";

        public static string EnsureUserConfig(string appBaseDirectory)
        {
            return EnsureUserConfig(appBaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }

        public static string EnsureUserConfig(string appBaseDirectory, string localAppDataRoot)
        {
            string root = string.IsNullOrWhiteSpace(localAppDataRoot)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : localAppDataRoot;

            if (string.IsNullOrWhiteSpace(root))
            {
                root = appBaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            }

            string userDir = Path.Combine(root, AppFolderName);
            string userConfigPath = Path.Combine(userDir, DefaultConfigFileName);
            if (File.Exists(userConfigPath))
            {
                return userConfigPath;
            }

            Directory.CreateDirectory(userDir);

            string templatePath = Path.Combine(appBaseDirectory ?? string.Empty, DefaultConfigFileName);
            if (File.Exists(templatePath))
            {
                File.Copy(templatePath, userConfigPath, overwrite: false);
                AppConfig.Save(userConfigPath, AppConfig.Load(userConfigPath));
            }
            else
            {
                AppConfig.Save(userConfigPath, AppConfig.CreateDefault());
            }

            return userConfigPath;
        }
    }
}
