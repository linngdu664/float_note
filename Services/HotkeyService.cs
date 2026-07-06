using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace FloatNote.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    private readonly HwndSource _source;
    private readonly Action _onHotkey;

    public HotkeyService(IntPtr windowHandle, Action onHotkey)
    {
        _source = HwndSource.FromHwnd(windowHandle)
                  ?? throw new InvalidOperationException("Unable to bind hotkey to window.");
        _onHotkey = onHotkey;
        _source.AddHook(WndProc);

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.N);
        RegisterHotKey(windowHandle, HotkeyId, ModControl | ModAlt, virtualKey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _onHotkey();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        var handle = _source.Handle;
        _source.RemoveHook(WndProc);
        UnregisterHotKey(handle, HotkeyId);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
