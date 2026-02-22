using System.Reflection;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.Win32;
using ImGuiNative = Hexa.NET.ImGui.ImGui;

namespace Teal.ImGui;

public sealed class ImGuiManager : IDisposable
{
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public delegate bool EventHandler();

    private IntPtr _previousWndProc = IntPtr.Zero;
    private WndProcDelegate? _newWndProc;
    private readonly NativeLibraryLoader _nativeLibraryLoader;
    private readonly List<IOverlay> _overlays = new();
    private readonly Lock _lock = new();
    private SwapBuffersHook? _swapBuffersHook;
    private ImGuiContextPtr _context;
    private bool _initialized;
    private IntPtr _windowHandle = IntPtr.Zero;

    public static ImGuiManager Instance { get; } = new();

    public event EventHandler? OnBuildFontAtlas;

    private ImGuiManager()
    {
        _nativeLibraryLoader = new NativeLibraryLoader();
    }

    public void RegisterRender(IOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        using (_lock.EnterScope())
        {
            if (!_overlays.Contains(overlay))
            {
                _overlays.Add(overlay);
            }
        }
    }

    public void UnregisterRender(IOverlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        using (_lock.EnterScope())
        {
            _overlays.Remove(overlay);
        }
    }

    public bool Initialize()
    {
        if (!LoadNativeLibraries())
        {
            return false;
        }

        try
        {
            _swapBuffersHook = new SwapBuffersHook();
            _swapBuffersHook.OnSwapBuffers += SwapBuffersHooked;
            _swapBuffersHook.Install();
        }
        catch
        {
            _nativeLibraryLoader.Dispose();
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        using (_lock.EnterScope())
        {
            _overlays.Clear();
        }

        RestoreWndProc();
        if (_swapBuffersHook != null)
        {
            _swapBuffersHook.OnSwapBuffers -= SwapBuffersHooked;
            _swapBuffersHook.Uninstall();
            _swapBuffersHook = null;
        }

        if (_initialized && _context != ImGuiContextPtr.Null)
        {
            ImGuiNative.SetCurrentContext(_context);
            ImGuiImplOpenGL3.Shutdown();
            ImGuiImplWin32.Shutdown();
        }

        if (_context != ImGuiContextPtr.Null)
        {
            ImGuiNative.DestroyContext(_context);
            _context = default;
        }

        _nativeLibraryLoader.Dispose();
        _initialized = false;
    }

    private bool SwapBuffersHooked(IntPtr hdc)
    {
        if (!_initialized)
        {
            if (!InitializeHook(hdc))
            {
                return _swapBuffersHook?.OriginalSwapBuffers?.Invoke(hdc) ?? true;
            }

            _initialized = true;
        }

        try
        {
            ImGuiImplOpenGL3.NewFrame();
            ImGuiImplWin32.NewFrame();
            ImGuiNative.NewFrame();

            List<IOverlay> overlays;
            using (_lock.EnterScope())
            {
                overlays = _overlays.ToList();
            }

            foreach (IOverlay overlay in overlays)
            {
                try
                {
                    overlay.Render();
                }
                catch
                {
                    
                }
            }

            ImGuiNative.Render();
            OpenGLInterop.glDisable(OpenGLInterop.GlFramebufferSrgb);
            ImGuiImplOpenGL3.RenderDrawData(ImGuiNative.GetDrawData());
            OpenGLInterop.glEnable(OpenGLInterop.GlFramebufferSrgb);
        }
        catch
        {
        }

        return _swapBuffersHook?.OriginalSwapBuffers?.Invoke(hdc) ?? true;
    }

    private bool InitializeHook(IntPtr hdc)
    {
        try
        {
            _context = ImGuiNative.CreateContext();
            if (_context == ImGuiContextPtr.Null)
            {
                return false;
            }

            _windowHandle = Win32Interop.WindowFromDC(hdc);
            if (_windowHandle == IntPtr.Zero)
            {
                ImGuiNative.DestroyContext(_context);
                _context = default;
                return false;
            }

            if (!SubclassWndProc())
            {
                ImGuiNative.DestroyContext(_context);
                _context = default;
                return false;
            }

            ImGuiNative.SetCurrentContext(_context);
            OnBuildFontAtlas?.Invoke();

            ImGuiImplWin32.SetCurrentContext(_context);
            if (!ImGuiImplWin32.InitForOpenGL(_windowHandle))
            {
                RestoreWndProc();
                ImGuiNative.DestroyContext(_context);
                _context = default;
                return false;
            }

            ImGuiImplOpenGL3.SetCurrentContext(_context);
            if (!ImGuiImplOpenGL3.Init("#version 330 core"))
            {
                RestoreWndProc();
                ImGuiImplWin32.Shutdown();
                ImGuiNative.DestroyContext(_context);
                _context = default;
                return false;
            }

            return true;
        }
        catch
        {
            RestoreWndProc();
            if (_context != ImGuiContextPtr.Null)
            {
                ImGuiNative.SetCurrentContext(_context);
                ImGuiImplOpenGL3.Shutdown();
                ImGuiImplWin32.Shutdown();
                ImGuiNative.DestroyContext(_context);
                _context = default;
            }

            return false;
        }
    }

    private bool SubclassWndProc()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (_previousWndProc != IntPtr.Zero)
        {
            return true;
        }

        _newWndProc = NewWndProc;
        IntPtr functionPointerForDelegate = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _previousWndProc = Win32Interop.SetWindowLongPtr(_windowHandle, Win32Interop.GwlWndProc, functionPointerForDelegate);
        if (_previousWndProc == IntPtr.Zero)
        {
            _newWndProc = null;
            return false;
        }

        return true;
    }

    private void RestoreWndProc()
    {
        if (_windowHandle != IntPtr.Zero && _previousWndProc != IntPtr.Zero)
        {
            IntPtr currentWndProc = Win32Interop.GetWindowLongPtr(_windowHandle, Win32Interop.GwlWndProc);
            IntPtr newPtr = _newWndProc != null ? Marshal.GetFunctionPointerForDelegate(_newWndProc) : IntPtr.Zero;
            if (currentWndProc == newPtr)
            {
                Win32Interop.SetWindowLongPtr(_windowHandle, Win32Interop.GwlWndProc, _previousWndProc);
            }

            _previousWndProc = IntPtr.Zero;
            _newWndProc = null;
        }
    }

    private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        IntPtr handled = ImGuiImplWin32.WndProcHandler(hWnd, msg, (nuint)(nint)wParam, lParam);
        ImGuiIOPtr io = ImGuiNative.GetIO();
        bool wantsInput = io.WantCaptureMouse || io.WantCaptureKeyboard;
        if (!wantsInput)
        {
            wantsInput = ImGuiNative.IsAnyItemActive() || ImGuiNative.IsAnyItemFocused();
        }

        if (wantsInput)
        {
            if (msg is >= 256 and <= 262 || msg is >= 512 and <= 522)
            {
                return IntPtr.Zero;
            }
        }

        if (handled != IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return _previousWndProc != IntPtr.Zero
            ? Win32Interop.CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam)
            : Win32Interop.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private bool LoadNativeLibraries()
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        IntPtr cimgui = _nativeLibraryLoader.LoadLibraryFromResource("cimgui.dll", executingAssembly);
        if (cimgui == IntPtr.Zero)
        {
            return false;
        }

        IntPtr impl = _nativeLibraryLoader.LoadLibraryFromResource("ImGuiImpl.dll", executingAssembly);
        if (impl == IntPtr.Zero)
        {
            _nativeLibraryLoader.Dispose();
            return false;
        }

        return true;
    }
}


