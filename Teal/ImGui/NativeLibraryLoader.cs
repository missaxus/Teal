using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Teal.ImGui;

internal sealed class NativeLibraryLoader : IDisposable
{
    private readonly Dictionary<string, (string TempPath, IntPtr Handle)> _libraries = new();
    private readonly Lock _lock = new();
    private string? _tempDirectory;

    public NativeLibraryLoader()
    {
        try
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
        }
        catch
        {
            _tempDirectory = null;
        }
    }

    public void Dispose()
    {
        using (_lock.EnterScope())
        {
            foreach (var entry in _libraries.Reverse())
            {
                var (filePath, handle) = entry.Value;
                if (!Win32Interop.FreeLibrary(handle))
                {
                    _ = Marshal.GetLastWin32Error();
                }

                TryDeleteFile(filePath);
            }

            _libraries.Clear();
        }

        if (string.IsNullOrEmpty(_tempDirectory) || !Directory.Exists(_tempDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempDirectory, true);
        }
        catch
        {
        }
        finally
        {
            _tempDirectory = null;
        }
    }

    public IntPtr LoadLibraryFromResource(string libraryFileName, Assembly callingAssembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryFileName);
        ArgumentNullException.ThrowIfNull(callingAssembly);

        if (string.IsNullOrEmpty(_tempDirectory))
        {
            return IntPtr.Zero;
        }

        using (_lock.EnterScope())
        {
            if (_libraries.TryGetValue(libraryFileName, out var value))
            {
                return value.Handle;
            }

            string? resourceName = callingAssembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(libraryFileName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(resourceName))
            {
                return IntPtr.Zero;
            }

            string tempPath = Path.Combine(_tempDirectory, libraryFileName);
            try
            {
                using Stream? stream = callingAssembly.GetManifestResourceStream(resourceName!);
                if (stream == null)
                {
                    return IntPtr.Zero;
                }

                using FileStream destination = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                stream.CopyTo(destination);
            }
            catch
            {
                TryDeleteFile(tempPath);
                return IntPtr.Zero;
            }

            IntPtr handle = Win32Interop.LoadLibrary(tempPath);
            if (handle == IntPtr.Zero)
            {
                _ = Marshal.GetLastWin32Error();
                TryDeleteFile(tempPath);
                return IntPtr.Zero;
            }

            _libraries[libraryFileName] = (tempPath, handle);
            return handle;
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            File.Delete(filePath);
        }
        catch
        {
        }
    }
}


