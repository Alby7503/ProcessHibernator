using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ProcessHibernator.Native;

namespace ProcessHibernator.Logic {
    public class ProcessManager {
        private readonly object _lock = new();
        private readonly HashSet<string> _suspendedApps = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _suspendedPids = [];
        private readonly Dictionary<int, string> _pidToAppName = [];
        private readonly Dictionary<int, List<string>> _suspendedTitles = [];
        private readonly Dictionary<string, DateTime> _lastActiveTimes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _originalRam = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string>? OnWake;
        public event Action<string>? OnHibernate;

        public bool IsSuspended(string appName) {
            lock (_lock) return _suspendedApps.Contains(appName);
        }

        public bool IsPidSuspended(int pid) {
            lock (_lock) return _suspendedPids.Contains(pid);
        }

        public string? GetAppNameFromPid(int pid) {
            lock (_lock) return _pidToAppName.GetValueOrDefault(pid);
        }

        public void UpdateLastActive(string appName) {
            lock (_lock) _lastActiveTimes[appName] = DateTime.Now;
        }

        public DateTime GetLastActive(string appName) {
            lock (_lock) return _lastActiveTimes.GetValueOrDefault(appName, DateTime.MinValue);
        }

        public long GetOriginalRam(string appName) {
            lock (_lock) return _originalRam.GetValueOrDefault(appName, 0);
        }

        public List<string> GetSuspendedApps() {
            lock (_lock) return _suspendedApps.ToList();
        }

        public void HibernateProcess(string targetName) {
            try {
                var procs = Process.GetProcessesByName(targetName);
                if (procs.Length == 0) return;

                var family = new List<Process>();
                foreach (var r in procs) {
                    family.Add(r);
                    foreach (int cid in GetAllChildIds(r.Id)) {
                        try { family.Add(Process.GetProcessById(cid)); } catch { }
                    }
                }

                long originalMem = 0;
                foreach (var p in family) {
                    try { originalMem += p.WorkingSet64; } catch { }
                    lock (_lock) {
                        _suspendedPids.Add(p.Id);
                        _pidToAppName[p.Id] = targetName;
                    }
                }

                foreach (var p in family) CacheProcessWindowTitles(p.Id);

                // UI THREAD EXCLUSION: Suspend all threads EXCEPT UI threads.
                foreach (var p in family) {
                    HashSet<uint> uiThreads = new();
                    NativeMethods.EnumWindows((hWnd, lParam) => {
                        uint wTid = NativeMethods.GetWindowThreadProcessId(hWnd, out uint wPid);
                        if (wPid == p.Id) uiThreads.Add(wTid);
                        return true;
                    }, IntPtr.Zero);

                    try {
                        foreach (ProcessThread thread in p.Threads) {
                            if (!uiThreads.Contains((uint)thread.Id)) {
                                IntPtr hThread = NativeMethods.OpenThread(0x0002, false, (uint)thread.Id);
                                if (hThread != IntPtr.Zero) {
                                    NativeMethods.SuspendThread(hThread);
                                    NativeMethods.CloseHandle(hThread);
                                }
                            }
                        }
                        NativeMethods.EmptyWorkingSet(p.Handle);
                    } catch { }
                }

                lock (_lock) {
                    _originalRam[targetName] = originalMem;
                    _suspendedApps.Add(targetName);
                }
                OnHibernate?.Invoke(targetName);
            } catch { }
        }

        public void PerformWake(string targetName, bool ghostMode) {
            IntPtr hJob = ghostMode ? CreateGhostJob() : IntPtr.Zero;
            HashSet<int> pids = [];
            lock (_lock) {
                foreach (var kv in _pidToAppName)
                    if (kv.Value.Equals(targetName, StringComparison.OrdinalIgnoreCase)) pids.Add(kv.Key);
            }

            foreach (int pid in pids) {
                try {
                    using var proc = Process.GetProcessById(pid);
                    if (hJob != IntPtr.Zero) NativeMethods.AssignProcessToJobObject(hJob, proc.Handle);

                    // Resume ALL threads
                    foreach (ProcessThread thread in proc.Threads) {
                        IntPtr hThread = NativeMethods.OpenThread(0x0002, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero) {
                            while (NativeMethods.ResumeThread(hThread) > 0) { }
                            NativeMethods.CloseHandle(hThread);
                        }
                    }

                    lock (_lock) {
                        _suspendedPids.Remove(pid);
                        _pidToAppName.Remove(pid);
                        _suspendedTitles.Remove(pid);
                    }
                } catch { }
            }

            lock (_lock) {
                _suspendedApps.Remove(targetName);
                _lastActiveTimes[targetName] = DateTime.Now;
                _originalRam.Remove(targetName);
            }
            OnWake?.Invoke(targetName);
        }

        public void ResumeAll() {
            List<string> apps;
            lock (_lock) apps = _suspendedApps.ToList();
            foreach (var a in apps) PerformWake(a, false);
        }

        public string? GetAppNameByTitleMatch(IntPtr hwnd) {
            IntPtr ptr = Marshal.AllocHGlobal(1024);
            try {
                int len = NativeMethods.GetWindowText(hwnd, ptr, 512);
                if (len <= 0) return null;
                string title = Marshal.PtrToStringUni(ptr, len) ?? "";
                lock (_lock) {
                    foreach (var entry in _suspendedTitles) {
                        if (entry.Value.Any(t => title.Contains(t, StringComparison.OrdinalIgnoreCase))) {
                            if (_pidToAppName.TryGetValue(entry.Key, out var name)) return name;
                        }
                    }
                }
            } finally { Marshal.FreeHGlobal(ptr); }
            return null;
        }

        public void CacheProcessWindowTitles(int pid) {
            NativeMethods.EnumWindows((hWnd, lParam) => {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint wPid);
                if (wPid == pid) {
                    IntPtr p = Marshal.AllocHGlobal(1024);
                    try {
                        int l = NativeMethods.GetWindowText(hWnd, p, 512);
                        if (l > 0) {
                            string t = Marshal.PtrToStringUni(p, l) ?? "";
                            lock (_lock) {
                                if (!_suspendedTitles.TryGetValue(pid, out var list))
                                    _suspendedTitles[pid] = list = new();
                                if (!list.Contains(t)) list.Add(t);
                            }
                        }
                    } finally { Marshal.FreeHGlobal(p); }
                }
                return true;
            }, IntPtr.Zero);
        }

        private static List<int> GetAllChildIds(int parentId) {
            List<int> c = new();
            IntPtr s = NativeMethods.CreateToolhelp32Snapshot(2, 0);
            try {
                PROCESSENTRY32 pe = new() { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
                if (NativeMethods.Process32First(s, ref pe)) {
                    do {
                        if (pe.th32ParentProcessID == (uint)parentId) c.Add((int)pe.th32ProcessID);
                    } while (NativeMethods.Process32Next(s, ref pe));
                }
            } finally { NativeMethods.CloseHandle(s); }
            return c;
        }

        private static IntPtr CreateGhostJob() {
            IntPtr j = NativeMethods.CreateJobObject(IntPtr.Zero, "Ghost_" + Guid.NewGuid());
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION l = new();
            l.BasicLimitInformation.LimitFlags = 0x00000100;
            l.ProcessMemoryLimit = 512 * 1024 * 1024;
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(l));
            Marshal.StructureToPtr(l, p, false);
            NativeMethods.SetInformationJobObject(j, 9, p, (uint)Marshal.SizeOf(l));
            Marshal.FreeHGlobal(p);
            return j;
        }

        public void StartupSuspendedCheck(HashSet<string> safeList) {
            var threadMap = new Dictionary<uint, List<uint>>();
            IntPtr hSnap = NativeMethods.CreateToolhelp32Snapshot(4, 0);
            if (hSnap != -1) {
                try {
                    THREADENTRY32 te = new() { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };
                    if (NativeMethods.Thread32First(hSnap, ref te)) {
                        do {
                            if (!threadMap.TryGetValue(te.th32OwnerProcessID, out var list))
                                threadMap[te.th32OwnerProcessID] = list = new();
                            list.Add(te.th32ThreadID);
                        } while (NativeMethods.Thread32Next(hSnap, ref te));
                    }
                } finally { NativeMethods.CloseHandle(hSnap); }
            }

            foreach (var p in Process.GetProcesses()) {
                try {
                    if (safeList.Contains(p.ProcessName) || p.ProcessName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (threadMap.TryGetValue((uint)p.Id, out var threads) && threads.Count > 0) {
                        bool allSuspended = true;
                        foreach (uint tid in threads) {
                            IntPtr hThread = NativeMethods.OpenThread(2, false, tid);
                            if (hThread != IntPtr.Zero) {
                                try {
                                    uint prevCount = NativeMethods.SuspendThread(hThread);
                                    NativeMethods.ResumeThread(hThread);
                                    if (prevCount == 0) { allSuspended = false; break; }
                                } finally { NativeMethods.CloseHandle(hThread); }
                            } else { allSuspended = false; break; }
                        }
                        if (allSuspended) {
                            lock (_lock) {
                                _suspendedApps.Add(p.ProcessName);
                                _suspendedPids.Add(p.Id);
                                _pidToAppName[p.Id] = p.ProcessName;
                                _originalRam[p.ProcessName] = p.WorkingSet64;
                                CacheProcessWindowTitles(p.Id);
                            }
                        }
                    }
                } catch { }
            }
        }
    }
}
