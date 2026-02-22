using System;
using System.Runtime.InteropServices;
using MinHook;

namespace Teal.ImGui;

internal sealed class SwapBuffersHook : IDisposable
{
    private GCHandle? _swapBuffersHandle;
    private HookEngine? _hookEngine;
    private bool _disposed;

    public OpenGLInterop.WglSwapBuffersFunc? OriginalSwapBuffers { get; private set; }

    public event OpenGLInterop.WglSwapBuffersFunc? OnSwapBuffers;

    public void Install()
    {
        if (_hookEngine != null)
        {
            return;
        }

        _hookEngine = new HookEngine();
        IntPtr address = OpenGLInterop.wglGetProcAddress("wglSwapBuffers");
        if (address == IntPtr.Zero)
        {
            IntPtr moduleHandle = OpenGLInterop.GetModuleHandle("opengl32.dll");
            if (moduleHandle != IntPtr.Zero)
            {
                address = OpenGLInterop.GetProcAddress(moduleHandle, "wglSwapBuffers");
            }

            if (address == IntPtr.Zero)
            {
                _hookEngine.Dispose();
                _hookEngine = null;
                throw new EntryPointNotFoundException("Could not locate wglSwapBuffers.");
            }
        }

        OpenGLInterop.WglSwapBuffersFunc detour = WglSwapBuffersDetour;
        _swapBuffersHandle = GCHandle.Alloc(detour);

        try
        {
            OriginalSwapBuffers = _hookEngine.CreateHook(address, detour);
            _hookEngine.EnableHooks();
        }
        catch
        {
            Uninstall();
            throw;
        }
    }

    public void Uninstall()
    {
        if (_hookEngine != null)
        {
            try
            {
                _hookEngine.DisableHooks();
            }
            catch
            {
            }

            _hookEngine.Dispose();
            _hookEngine = null;
            OriginalSwapBuffers = null;
        }

        if (_swapBuffersHandle.HasValue && _swapBuffersHandle.Value.IsAllocated)
        {
            _swapBuffersHandle.Value.Free();
            _swapBuffersHandle = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Uninstall();
        _disposed = true;
    }

    private bool WglSwapBuffersDetour(IntPtr hdc)
    {
        if (OnSwapBuffers != null)
        {
            try
            {
                return OnSwapBuffers(hdc);
            }
            catch
            {
                return false;
            }
        }

        return OriginalSwapBuffers?.Invoke(hdc) ?? false;
    }
}


