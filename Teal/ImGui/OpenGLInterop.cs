using System;
using System.Runtime.InteropServices;

namespace Teal.ImGui;

internal static class OpenGLInterop
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate bool WglSwapBuffersFunc(IntPtr hdc);

    public const uint GlFramebufferSrgb = 36281u;

    [DllImport("opengl32.dll")]
    public static extern void glEnable(uint cap);

    [DllImport("opengl32.dll")]
    public static extern void glDisable(uint cap);

    [DllImport("opengl32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr wglGetProcAddress(string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
}


