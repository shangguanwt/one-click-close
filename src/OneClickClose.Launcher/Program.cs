using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OneClickClose.Launcher;

internal static class Program
{
    private const string AppFolderName = "app";
    private const string WinUiExecutableName = "OneClickClose.WinUI.exe";
    private const uint MbIconError = 0x00000010;

    [STAThread]
    private static int Main(string[] args)
    {
        string launcherDirectory = AppContext.BaseDirectory;
        string appDirectory = Path.Combine(launcherDirectory, AppFolderName);
        string appExecutable = Path.Combine(appDirectory, WinUiExecutableName);

        if (!File.Exists(appExecutable))
        {
            ShowError(
                "无法启动一键关闭",
                "未找到主程序：\n\n" + appExecutable + "\n\n请确认 app 文件夹和 OneClickClose.WinUI.exe 与启动器放在同一个解压目录中。");
            return 2;
        }

        try
        {
            using Process process = new Process();
            process.StartInfo.FileName = appExecutable;
            process.StartInfo.WorkingDirectory = appDirectory;
            process.StartInfo.UseShellExecute = false;

            foreach (string arg in args)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            process.Start();
            return 0;
        }
        catch (Win32Exception ex)
        {
            ShowError("无法启动一键关闭", "启动主程序失败：\n\n" + ex.Message);
            return ex.NativeErrorCode == 0 ? 3 : ex.NativeErrorCode;
        }
        catch (Exception ex)
        {
            ShowError("无法启动一键关闭", "启动主程序时发生异常：\n\n" + ex.Message);
            return 3;
        }
    }

    private static void ShowError(string title, string message)
    {
        _ = MessageBoxW(IntPtr.Zero, message, title, MbIconError);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
