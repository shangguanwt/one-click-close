using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using OneClickClose.Core;

namespace OneClickClose
{
    internal static class Program
    {
        public const string AppName = "OneClickClose";
        public const string DisplayName = "一键关闭后台软件";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(appDir, "close-user-apps.config.json");
            AppConfig.EnsureConfig(configPath);

            if (HasArg(args, "--validate"))
            {
                ProcessPlanner.GetClosePlan(configPath);
                return;
            }

            if (HasArg(args, "--validate-ui"))
            {
                using (MainForm form = new MainForm(configPath))
                {
                    form.CreateControl();
                }
                return;
            }

            string snapshotPath = GetArgValue(args, "--snapshot");
            if (!string.IsNullOrWhiteSpace(snapshotPath))
            {
                SaveSnapshot(configPath, snapshotPath);
                return;
            }

            if (HasArg(args, "--preview"))
            {
                MessageBox.Show(ProcessPlanner.FormatPlan(ProcessPlanner.GetClosePlan(configPath)), DisplayName);
                return;
            }

            Application.Run(new MainForm(configPath));
        }

        private static bool HasArg(string[] args, string expected)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetArgValue(string[] args, string expected)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static void SaveSnapshot(string configPath, string outputPath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (MainForm form = new MainForm(configPath))
            {
                form.SuppressAutoScan = true;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                form.LoadPlanForSnapshot(ProcessPlanner.GetClosePlan(configPath));
                Application.DoEvents();

                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(outputPath, ImageFormat.Png);
                }

                form.Close();
            }
        }
    }
}
