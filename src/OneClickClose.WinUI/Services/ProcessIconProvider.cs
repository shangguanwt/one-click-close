using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OneClickClose.Core;
using Windows.Storage.Streams;

namespace OneClickClose.WinUI.Services;

public static class ProcessIconProvider
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, byte[]> ByteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    public static ImageSource GetIconSource(ProcessGroupRow row)
    {
        if (row == null)
        {
            return null;
        }

        return GetIconSource(SelectIconPath(row.Path, row.Children));
    }

    public static ImageSource GetIconSource(ProcessRecord record)
    {
        return record == null ? null : GetIconSource(record.Path);
    }

    public static async Task<ImageSource> GetIconSourceAsync(ProcessGroupRow row)
    {
        if (row == null)
        {
            return null;
        }

        byte[] bytes = await Task.Run(() => GetIconBytes(BuildIconPathCandidates(row.Path, row.Children)));
        if (bytes == null)
        {
            return null;
        }

        return await CreateBitmapImageAsync(bytes);
    }

    private static ImageSource GetIconSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        string key;
        try
        {
            key = Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }

        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out ImageSource cached))
            {
                return cached;
            }

            if (MissingIconCache.Contains(key))
            {
                return null;
            }
        }

        ImageSource source = TryLoadAssociatedIcon(key) ?? TryLoadShellIcon(key);
        lock (CacheLock)
        {
            if (source != null)
            {
                Cache[key] = source;
            }
            else
            {
                MissingIconCache.Add(key);
            }
        }

        return source;
    }

    private static IEnumerable<string> BuildIconPathCandidates(string primaryPath, IReadOnlyList<ProcessRecord> children)
    {
        if (!string.IsNullOrWhiteSpace(primaryPath))
        {
            yield return primaryPath;
        }

        if (children == null)
        {
            yield break;
        }

        foreach (ProcessRecord child in children)
        {
            if (!string.IsNullOrWhiteSpace(child?.Path))
            {
                yield return child.Path;
            }
        }
    }

    private static byte[] GetIconBytes(IEnumerable<string> paths)
    {
        foreach (string path in paths ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string key;
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                key = Path.GetFullPath(path);
            }
            catch
            {
                continue;
            }

            lock (CacheLock)
            {
                if (ByteCache.TryGetValue(key, out byte[] cached))
                {
                    return cached;
                }

                if (MissingIconCache.Contains(key))
                {
                    continue;
                }
            }

            byte[] bytes = TryLoadAssociatedIconBytes(key) ?? TryLoadShellIconBytes(key);
            lock (CacheLock)
            {
                if (bytes != null)
                {
                    ByteCache[key] = bytes;
                    return bytes;
                }

                MissingIconCache.Add(key);
            }
        }

        return null;
    }

    private static string SelectIconPath(string primaryPath, IReadOnlyList<ProcessRecord> children)
    {
        if (!string.IsNullOrWhiteSpace(primaryPath) && File.Exists(primaryPath))
        {
            return primaryPath;
        }

        if (children == null)
        {
            return null;
        }

        foreach (ProcessRecord child in children)
        {
            if (!string.IsNullOrWhiteSpace(child?.Path) && File.Exists(child.Path))
            {
                return child.Path;
            }
        }

        return null;
    }

    private static ImageSource TryLoadAssociatedIcon(string path)
    {
        try
        {
            using System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null)
            {
                return null;
            }

            return CreateBitmapImage(icon);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] TryLoadAssociatedIconBytes(string path)
    {
        try
        {
            using System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            return icon == null ? null : CreatePngBytes(icon);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource TryLoadShellIcon(string path)
    {
        try
        {
            SHFILEINFO info = new();
            IntPtr result = SHGetFileInfo(
                path,
                0,
                ref info,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                ShgfiIcon | ShgfiLargeIcon);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(info.hIcon).Clone();
                return CreateBitmapImage(icon);
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static byte[] TryLoadShellIconBytes(string path)
    {
        try
        {
            SHFILEINFO info = new();
            IntPtr result = SHGetFileInfo(
                path,
                0,
                ref info,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                ShgfiIcon | ShgfiLargeIcon);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(info.hIcon).Clone();
                return CreatePngBytes(icon);
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage CreateBitmapImage(System.Drawing.Icon icon)
    {
        using System.Drawing.Bitmap bitmap = icon.ToBitmap();
        using var memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        return CreateBitmapImage(memory.ToArray());
    }

    private static byte[] CreatePngBytes(System.Drawing.Icon icon)
    {
        using System.Drawing.Bitmap bitmap = icon.ToBitmap();
        using var memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        return memory.ToArray();
    }

    private static async Task<BitmapImage> CreateBitmapImageAsync(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync().AsTask();
            await writer.FlushAsync().AsTask();
        }

        stream.Seek(0);
        image.SetSource(stream);
        return image;
    }

    private static BitmapImage CreateBitmapImage(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.FlushAsync().AsTask().GetAwaiter().GetResult();
        }

        stream.Seek(0);
        image.SetSource(stream);
        return image;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
