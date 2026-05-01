using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ProcessHibernator.Native;

namespace ProcessHibernator.Logic {
    public class InteractionEngine : IDisposable {
        private readonly ProcessManager _processManager;
        private readonly System.Threading.Timer _bgWatcher;
        private readonly NativeMethods.WinEventProc _winEventDelegate;
        private readonly IntPtr _hHookForeground;
        private readonly IntPtr _hHookFocus;
        private readonly Dictionary<uint, string> _activePidCache = [];
        private readonly object _cacheLock = new();

        public InteractionEngine(ProcessManager processManager) {
            _processManager = processManager;

            _winEventDelegate = WinEventCallback;
            _hHookForeground = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND, 
                IntPtr.Zero, _winEventDelegate, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
            
            _hHookFocus = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_FOCUS, NativeMethods.EVENT_OBJECT_FOCUS, 
                IntPtr.Zero, _winEventDelegate, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

            _bgWatcher = new System.Threading.Timer(BackgroundWatcherCallback, null, 0, 10);
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) {
            if (hwnd != IntPtr.Zero) CheckAndWakeHwnd(hwnd);
        }

        private void BackgroundWatcherCallback(object? state) {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero) CheckAndWakeHwnd(fg);

            if (NativeMethods.GetCursorPos(out POINT pt)) {
                IntPtr mouseHwnd = NativeMethods.WindowFromPoint(pt);
                if (mouseHwnd != IntPtr.Zero) CheckAndWakeHwnd(mouseHwnd);
            }

            lock (_cacheLock) {
                if (_activePidCache.Count > 100) _activePidCache.Clear();
            }
        }

        private void CheckAndWakeHwnd(IntPtr hwnd) {
            IntPtr root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root == IntPtr.Zero) root = hwnd;

            NativeMethods.GetWindowThreadProcessId(root, out uint pid);
            if (pid == 0) return;

            string? toWake = null;
            string? pName = null;

            // 1. FAST PID LOOKUP
            if (_processManager.IsPidSuspended((int)pid)) {
                toWake = _processManager.GetAppNameFromPid((int)pid);
            }

            // 2. NAME LOOKUP
            if (toWake == null) {
                lock (_cacheLock) {
                    _activePidCache.TryGetValue(pid, out pName);
                }

                if (string.IsNullOrEmpty(pName)) {
                    try {
                        using var p = Process.GetProcessById((int)pid);
                        pName = p.ProcessName;
                        lock (_cacheLock) { _activePidCache[pid] = pName; }
                    } catch { }
                }

                if (!string.IsNullOrEmpty(pName) && _processManager.IsSuspended(pName)) {
                    toWake = pName;
                }
            }

            // 3. TITLE LOOKUP
            if (toWake == null) {
                toWake = _processManager.GetAppNameByTitleMatch(hwnd);
            }

            if (toWake != null) {
                _processManager.PerformWake(toWake, false);
            } else if (!string.IsNullOrEmpty(pName)) {
                _processManager.UpdateLastActive(pName);
            }
        }

        public void Dispose() {
            if (_hHookForeground != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hHookForeground);
            if (_hHookFocus != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hHookFocus);
            _bgWatcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
