using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OneClickClose.Setup
{
    internal static class SetupProgram
    {
        public const string AppName = "OneClickClose";
        public const string DisplayName = "一键关闭后台软件";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (HasArg(args, "--uninstall"))
            {
                Uninstall();
                return;
            }

            if (HasArg(args, "--install"))
            {
                bool quiet = HasArg(args, "--quiet");
                Install(delegate(string message)
                {
                    if (!quiet)
                    {
                        MessageBox.Show(message, DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });

                if (!quiet)
                {
                    MessageBox.Show("安装完成。", DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            Application.Run(new SetupForm());
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

        public static string InstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        }

        public static string StartMenuDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);
        }

        public static string DesktopShortcut()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DisplayName + ".lnk");
        }

        public static void Install(Action<string> log)
        {
            string dir = InstallDir();
            Directory.CreateDirectory(dir);
            log("安装目录：" + dir);

            string appExe = Path.Combine(dir, "OneClickClose.exe");
            string config = Path.Combine(dir, "close-user-apps.config.json");
            string defaultConfig = Path.Combine(dir, "close-user-apps.default.json");
            string setupExe = Path.Combine(dir, "OneClickCloseSetup.exe");

            CloseRunningInstances(appExe, log);
            ExtractResource("OneClickClose.exe", appExe, true);
            ExtractResource("close-user-apps.config.json", defaultConfig, true);
            if (!File.Exists(config))
            {
                File.Copy(defaultConfig, config, true);
            }

            if (!string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(setupExe), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(Application.ExecutablePath, setupExe, true);
            }
            log("已复制程序文件。");

            CreateShortcut(DesktopShortcut(), appExe, "", dir, DisplayName, appExe + ",0");
            Directory.CreateDirectory(StartMenuDir());
            CreateShortcut(Path.Combine(StartMenuDir(), DisplayName + ".lnk"), appExe, "", dir, DisplayName, appExe + ",0");
            CreateShortcut(Path.Combine(StartMenuDir(), "卸载 " + DisplayName + ".lnk"), setupExe, "--uninstall", dir, "卸载 " + DisplayName, setupExe + ",0");
            log("已创建桌面和开始菜单快捷方式。");

            WriteUninstallRegistry(setupExe, dir);
            log("已写入当前用户卸载信息。");
        }

        public static void Uninstall()
        {
            DialogResult choice = MessageBox.Show("确认卸载“一键关闭后台软件”？\n\n会删除快捷方式和安装目录。", "卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice != DialogResult.Yes)
            {
                return;
            }

            try
            {
                DeleteIfExists(DesktopShortcut());
                string startMenu = StartMenuDir();
                if (Directory.Exists(startMenu))
                {
                    Directory.Delete(startMenu, true);
                }
                RemoveUninstallRegistry();

                string dir = InstallDir();
                string self = Application.ExecutablePath;
                string cmd = "/c ping 127.0.0.1 -n 2 > nul & rmdir /s /q \"" + dir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                MessageBox.Show("卸载已开始。安装目录会在窗口关闭后删除。", "卸载", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "卸载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ExtractResource(string resourceName, string destination, bool overwrite)
        {
            if (File.Exists(destination) && !overwrite)
            {
                return;
            }

            IOException lastIOException = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using (Stream input = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (input == null)
                        {
                            throw new InvalidOperationException("安装器缺少资源：" + resourceName);
                        }

                        using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write))
                        {
                            input.CopyTo(output);
                        }
                    }
                    return;
                }
                catch (IOException ex)
                {
                    lastIOException = ex;
                    Thread.Sleep(500);
                }
            }

            throw lastIOException ?? new IOException("无法写入文件：" + destination);
        }

        private static void CloseRunningInstances(string appExe, Action<string> log)
        {
            foreach (Process process in Process.GetProcessesByName("OneClickClose"))
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id)
                    {
                        continue;
                    }

                    string path = process.MainModule == null ? "" : (process.MainModule.FileName ?? "");
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(appExe), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    log("正在关闭旧版本进程：" + process.Id);
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        process.CloseMainWindow();
                        process.WaitForExit(2000);
                    }

                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
                catch
                {
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string description, string iconLocation)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments ?? "";
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Description = description;
            shortcut.IconLocation = iconLocation;
            shortcut.Save();
        }

        private static void WriteUninstallRegistry(string setupExe, string installDir)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                key.SetValue("DisplayName", DisplayName);
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "OneClickClose");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", Path.Combine(installDir, "OneClickClose.exe"));
                key.SetValue("UninstallString", "\"" + setupExe + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static void RemoveUninstallRegistry()
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName, false);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public sealed class SetupForm : Form
    {
        private TextBox logBox;
        private Button installButton;
        private Button launchButton;
        private readonly Color background = Color.FromArgb(244, 240, 234);
        private readonly Color text = Color.FromArgb(28, 37, 36);
        private readonly Color muted = Color.FromArgb(111, 101, 93);
        private readonly Color primary = Color.FromArgb(15, 118, 110);

        public SetupForm()
        {
            Text = SetupProgram.DisplayName + " 安装器";
            Width = 640;
            Height = 430;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = background;
            Font = new Font("Microsoft YaHei UI", 9F);

            Label title = new Label();
            title.Text = "安装一键关闭后台软件";
            title.Font = new Font(Font.FontFamily, 20F, FontStyle.Bold);
            title.ForeColor = text;
            title.AutoSize = true;
            title.Location = new Point(28, 24);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "安装到当前用户目录，创建桌面和开始菜单快捷方式，不需要管理员权限。";
            subtitle.ForeColor = muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(31, 72);
            Controls.Add(subtitle);

            installButton = MakeButton("安装 / 更新", primary, Color.White, 130);
            installButton.Location = new Point(31, 112);
            installButton.Click += InstallButtonClick;
            Controls.Add(installButton);

            launchButton = MakeButton("打开软件", Color.FromArgb(217, 230, 223), text, 110);
            launchButton.Location = new Point(174, 112);
            launchButton.Enabled = File.Exists(Path.Combine(SetupProgram.InstallDir(), "OneClickClose.exe"));
            launchButton.Click += delegate { LaunchInstalledApp(); };
            Controls.Add(launchButton);

            Button folderButton = MakeButton("安装目录", Color.FromArgb(238, 232, 222), text, 110);
            folderButton.Location = new Point(297, 112);
            folderButton.Click += delegate { OpenInstallFolder(); };
            Controls.Add(folderButton);

            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BackColor = Color.FromArgb(251, 250, 247);
            logBox.ForeColor = Color.FromArgb(36, 48, 47);
            logBox.Font = new Font("Consolas", 9F);
            logBox.Location = new Point(31, 170);
            logBox.Size = new Size(560, 180);
            Controls.Add(logBox);

            AppendLog("准备就绪。");
        }

        private Button MakeButton(string caption, Color backColor, Color foreColor, int width)
        {
            Button button = new Button();
            button.Text = caption;
            button.Width = width;
            button.Height = 40;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
            return button;
        }

        private void InstallButtonClick(object sender, EventArgs e)
        {
            installButton.Enabled = false;
            try
            {
                SetupProgram.Install(AppendLog);
                launchButton.Enabled = true;
                AppendLog("安装完成。");
                MessageBox.Show("安装完成。桌面和开始菜单里都可以打开它。", "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("安装失败：" + ex.Message);
                MessageBox.Show(ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                installButton.Enabled = true;
            }
        }

        private void LaunchInstalledApp()
        {
            string app = Path.Combine(SetupProgram.InstallDir(), "OneClickClose.exe");
            if (File.Exists(app))
            {
                Process.Start(app);
            }
            else
            {
                MessageBox.Show("尚未安装。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenInstallFolder()
        {
            Directory.CreateDirectory(SetupProgram.InstallDir());
            Process.Start("explorer.exe", SetupProgram.InstallDir());
        }

        private void AppendLog(string message)
        {
            if (logBox.TextLength > 0)
            {
                logBox.AppendText(Environment.NewLine);
            }
            logBox.AppendText(message);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }
    }
}
