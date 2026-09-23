using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskLofi.Services;
/// <summary>Global low-level hooks report activity only. Key data is discarded immediately.</summary>
public sealed class InputMonitor : IDisposable
{
    public event Action<DateTime>? KeyboardActivity;
    public event Action<DateTime>? MouseActivity;
    private IntPtr _keyboard, _mouse;
    private readonly HookProc _keyProc, _mouseProc;
    private bool _disposed;
    public bool IsKeyboardHookInstalled => _keyboard != IntPtr.Zero;
    public bool IsMouseHookInstalled => _mouse != IntPtr.Zero;
    private long _lastMouseActivityTicks;
    private const int WhKeyboardLl = 13, WhMouseLl = 14, WmKeyDown = 0x100, WmSysKeyDown = 0x104, WmMouseMove = 0x200, WmLButtonDown = 0x201, WmRButtonDown = 0x204, WmMouseWheel = 0x20A;
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Point Point; public uint MouseDataValue, Flags, Time; public IntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? name);
    public InputMonitor()
    {
        _keyProc = KeyCallback; _mouseProc = MouseCallback; var module = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName);
        _keyboard = SetWindowsHookEx(WhKeyboardLl, _keyProc, module, 0); _mouse = SetWindowsHookEx(WhMouseLl, _mouseProc, module, 0);
        if(_keyboard==IntPtr.Zero)LoggerService.Error($"Keyboard activity hook installation failed (win32 {Marshal.GetLastWin32Error()}).");
        if(_mouse==IntPtr.Zero)LoggerService.Error($"Mouse activity hook installation failed (win32 {Marshal.GetLastWin32Error()}).");
    }
    private IntPtr KeyCallback(int code, IntPtr w, IntPtr l) { if (code >= 0 && (w.ToInt32() == WmKeyDown || w.ToInt32() == WmSysKeyDown)) KeyboardActivity?.Invoke(DateTime.UtcNow); return CallNextHookEx(_keyboard, code, w, l); }
    private IntPtr MouseCallback(int code, IntPtr w, IntPtr l) { int m = w.ToInt32(); if (code >= 0 && (m == WmMouseMove || m == WmLButtonDown || m == WmRButtonDown || m == WmMouseWheel)) { long now=Environment.TickCount64, previous=Interlocked.Read(ref _lastMouseActivityTicks); if (m != WmMouseMove || now-previous>=150) { Interlocked.Exchange(ref _lastMouseActivityTicks,now); MouseActivity?.Invoke(DateTime.UtcNow); } } return CallNextHookEx(_mouse, code, w, l); }
    public void Dispose() { if (_disposed) return; _disposed = true; if (_keyboard != IntPtr.Zero) UnhookWindowsHookEx(_keyboard); if (_mouse != IntPtr.Zero) UnhookWindowsHookEx(_mouse); _keyboard = _mouse = IntPtr.Zero; GC.KeepAlive(_keyProc); GC.KeepAlive(_mouseProc); }
}
