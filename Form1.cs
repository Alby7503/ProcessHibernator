using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ProcessHibernator {
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    public partial class Form1 : Form {
        // --- WIN32 API SECTION ---
        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool EmptyWorkingSet(IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct PROCESSENTRY32 {
            public uint dwSize; public uint cntUsage; public uint th32ProcessID;
            public IntPtr th32DefaultHeapID; public uint th32ModuleID; public uint cntThreads;
            public uint th32ParentProcessID; public int pcPriClassBase; public uint dwFlags;
            public fixed byte szExeFile[260];
        };

        [DllImport("kernel32.dll", SetLastError = true)]
        protected static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, IntPtr lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_FOCUS = 0x8005;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint GA_ROOT = 2;
        private const uint GW_HWNDNEXT = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX {
            public uint dwLength; public uint dwMemoryLoad; ulong ullTotalPhys; ulong ullAvailPhys; ulong ullTotalPageFile; ulong ullAvailPageFile; ulong ullTotalVirtual; ulong ullAvailVirtual; ulong ullAvailExtendedVirtual;
            public static MEMORYSTATUSEX Create() => new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            public ulong TotalPhys => ullTotalPhys;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateJobObjectW")]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [StructLayout(LayoutKind.Sequential)]
        public struct THREADENTRY32 {
            public uint dwSize; public uint cntUsage; public uint th32ThreadID; public uint th32OwnerProcessID; public int tpBasePri; public int tpDeltaPri; public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoCounters; public nuint ProcessMemoryLimit; public nuint JobMemoryLimit; public nuint PeakProcessMemoryUsed; public nuint PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
            public long PerProcessUserTimeLimit; public long PerJobUserTimeLimit; public uint LimitFlags; public nuint MinimumWorkingSetSize; public nuint MaximumWorkingSetSize; public uint ActiveProcessLimit; public nuint Affinity; public uint PriorityClass; public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS { public ulong ReadOperationCount; public ulong WriteOperationCount; public ulong OtherOperationCount; public ulong ReadTransferCount; public ulong WriteTransferCount; public ulong OtherTransferCount; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        // --- APP STATE ---
        private readonly object _lock = new();
        private readonly HashSet<string> _safeList = new(StringComparer.OrdinalIgnoreCase) { "explorer", "dwm", "taskmgr", "logonui", "devenv", "ProcessHibernator", "discord" };
        private readonly HashSet<string> _suspendedApps = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _suspendedPids = [];
        private readonly Dictionary<int, string> _pidToAppName = [];
        private readonly Dictionary<int, List<string>> _suspendedTitles = [];
        private readonly Dictionary<string, DateTime> _lastActiveTimes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _originalRam = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<long> _memHistory = [];
        private const int MaxHistory = 50;
        private const double AutoHibernateSeconds = 10;
        private readonly Dictionary<string, Icon?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private CheckBox? _chkEcoPilot;
        private System.Windows.Forms.Timer _interactionWatcher = new();
        private IntPtr _hHook;
        private IntPtr _hHookFocus;
        private WinEventProc? _winEventDelegate;

        private record ProcessData(string Name, long Memory, string? FilePath, long OriginalMemory, DateTime LastActive);

        public class ProcessItem {
            public string Name { get; set; } = "";
            public long Memory { get; set; }
            public long OriginalMemory { get; set; }
            public DateTime LastActive { get; set; }
            public Icon? Icon { get; set; }
            public override string ToString() => Name;
        }

        public Form1() {
            InitializeComponent();
            SetupEcoPilotUI();
            TrayIcon.Icon = this.Icon;

            // Global Hook to catch when a window naturally becomes foreground AND when it receives focus in Alt-Tab
            _winEventDelegate = WinEventCallback;
            _hHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
            _hHookFocus = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            StartupSuspendedCheck();

            _interactionWatcher.Interval = 50; 
            _interactionWatcher.Tick += InteractionWatcher_Tick;
            _interactionWatcher.Start();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => ResumeAll();
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) {
            if (hwnd != IntPtr.Zero) CheckAndWakeHwnd(hwnd);
        }

        private void CheckAndWakeHwnd(IntPtr hwnd) {
            if (_chkEcoPilot == null || !_chkEcoPilot.Checked) return;

            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            if (root == IntPtr.Zero) root = hwnd;

            GetWindowThreadProcessId(root, out uint pid);
            if (pid == 0) return;

            string? toWake = null;

            lock (_lock) {
                // 1. FAST PID LOOKUP (Instantly reliable)
                if (_suspendedPids.Contains((int)pid)) {
                    _pidToAppName.TryGetValue((int)pid, out toWake);
                }
            }

            // 2. NAME LOOKUP (Using .NET cache to avoid Win32 permission errors)
            string pName = "";
            if (toWake == null) {
                try {
                    using var p = Process.GetProcessById((int)pid);
                    pName = p.ProcessName;
                } catch { }
                if (!string.IsNullOrEmpty(pName)) {
                    lock (_lock) { if (_suspendedApps.Contains(pName)) toWake = pName; }
                }
            }

            // 3. TITLE LOOKUP (Ghost fallback)
            if (toWake == null) {
                toWake = GetAppNameByTitleMatch(hwnd);
            }

            if (toWake != null) {
                this.BeginInvoke(() => {
                    PerformWake(toWake, false);
                    LblStatus.Text = $"Eco-Pilot: {toWake} risvegliato via UI Exclusion.";
                });
            } else if (!string.IsNullOrEmpty(pName)) {
                // Update active time to prevent premature hibernation
                lock (_lock) { _lastActiveTimes[pName] = DateTime.Now; }
            }
        }

        private void InteractionWatcher_Tick(object? sender, EventArgs e) {
            if (_chkEcoPilot == null || !_chkEcoPilot.Checked) return;

            // 1. Z-ORDER RAYCASTING
            // Explorer brings the target window to the top of the Z-Order BEFORE sending WM_ACTIVATE.
            IntPtr current = GetTopWindow(IntPtr.Zero);
            for (int i = 0; i < 15 && current != IntPtr.Zero; i++) {
                if (IsWindowVisible(current)) CheckAndWakeHwnd(current);
                current = GetWindow(current, GW_HWNDNEXT);
            }

            // 2. Check Foreground Window
            IntPtr fg = GetForegroundWindow();
            if (fg != IntPtr.Zero) CheckAndWakeHwnd(fg);

            // 3. Fast Hover Detection
            if (GetCursorPos(out POINT pt)) {
                IntPtr mouseHwnd = WindowFromPoint(pt);
                if (mouseHwnd != IntPtr.Zero) CheckAndWakeHwnd(mouseHwnd);
            }
        }

        private string? GetAppNameByTitleMatch(IntPtr hwnd) {
            IntPtr ptr = Marshal.AllocHGlobal(1024);
            try {
                int len = GetWindowText(hwnd, ptr, 512);
                if (len <= 0) return null;
                string title = Marshal.PtrToStringUni(ptr, len) ?? "";
                lock (_lock) {
                    foreach (var entry in _suspendedTitles) {
                        if (entry.Value.Any(t => title.Contains(t, StringComparison.OrdinalIgnoreCase))) {
                            if (_pidToAppName.TryGetValue(entry.Key, out var name)) return name;
                        }
                    }
                }
            }
            finally { Marshal.FreeHGlobal(ptr); }
            return null;
        }

        private void SetupEcoPilotUI() {
            _chkEcoPilot = new CheckBox { Text = "Eco-Pilot (Auto)", ForeColor = Color.LimeGreen, AutoSize = true, Location = new Point(408, 385), FlatStyle = FlatStyle.Flat };
            this.Controls.Add(_chkEcoPilot);
            LbApps.DrawMode = DrawMode.OwnerDrawFixed; LbSuspended.DrawMode = DrawMode.OwnerDrawFixed;
            LbApps.SelectionMode = SelectionMode.MultiExtended; LbSuspended.SelectionMode = SelectionMode.MultiExtended;
            LbApps.DrawItem += Lb_DrawItem; LbSuspended.DrawItem += Lb_DrawItem;
        }

        private void BtnHibernate_Click(object sender, EventArgs e) { 
            foreach (ProcessItem item in LbApps.SelectedItems.Cast<ProcessItem>().ToList()) HibernateProcess(item.Name); 
        }
        private void BtnWake_Click(object sender, EventArgs e) { 
            foreach (ProcessItem item in LbSuspended.SelectedItems.Cast<ProcessItem>().ToList()) PerformWake(item.Name, false); 
        }
        private void BtnGhostWake_Click(object sender, EventArgs e) { 
            foreach (ProcessItem item in LbSuspended.SelectedItems.Cast<ProcessItem>().ToList()) PerformWake(item.Name, true); 
        }
        private void BtnKill_Click(object sender, EventArgs e) {
            var selectedApps = LbApps.SelectedItems.Cast<ProcessItem>().ToList();
            var selectedSuspended = LbSuspended.SelectedItems.Cast<ProcessItem>().ToList();
            var allSelected = selectedApps.Concat(selectedSuspended).ToList();
            
            if (allSelected.Count > 0) {
                string names = string.Join(", ", allSelected.Select(p => p.Name));
                if (MessageBox.Show($"Kill {names}?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                    foreach (var pi in allSelected) {
                        foreach (var p in Process.GetProcessesByName(pi.Name)) try { p.Kill(true); } catch { }
                        lock (_lock) _suspendedApps.Remove(pi.Name);
                    }
                    UpdateLists();
                }
            }
        }

        public void HibernateProcess(string targetName) {
            try {
                var procs = Process.GetProcessesByName(targetName);
                if (procs.Length == 0) return;
                var family = new List<Process>();
                foreach (var r in procs) { family.Add(r); foreach (int cid in GetAllChildIds(r.Id)) try { family.Add(Process.GetProcessById(cid)); } catch { } }
                
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
                // This prevents Explorer from hanging during Alt-Tab and Taskbar clicks.
                foreach (var p in family) {
                    HashSet<uint> uiThreads = new();
                    EnumWindows((hWnd, lParam) => {
                        uint wTid = GetWindowThreadProcessId(hWnd, out uint wPid);
                        if (wPid == p.Id) uiThreads.Add(wTid);
                        return true;
                    }, IntPtr.Zero);

                    try {
                        foreach (ProcessThread thread in p.Threads) {
                            if (!uiThreads.Contains((uint)thread.Id)) {
                                IntPtr hThread = OpenThread(0x0002, false, (uint)thread.Id);
                                if (hThread != IntPtr.Zero) {
                                    SuspendThread(hThread);
                                    CloseHandle(hThread);
                                }
                            }
                        }
                        EmptyWorkingSet(p.Handle);
                    } catch { }
                }

                lock (_lock) { _originalRam[targetName] = originalMem; _suspendedApps.Add(targetName); } 
                UpdateLists();
            } catch { }
        }

        public void PerformWake(string targetName, bool ghostMode, bool skipUpdate = false) {
            IntPtr hJob = ghostMode ? CreateGhostJob() : IntPtr.Zero;
            HashSet<int> pids = [];
            lock (_lock) { foreach (var kv in _pidToAppName) if (kv.Value.Equals(targetName, StringComparison.OrdinalIgnoreCase)) pids.Add(kv.Key); }
            foreach (int pid in pids) {
                try {
                    using var proc = Process.GetProcessById(pid);
                    if (hJob != IntPtr.Zero) AssignProcessToJobObject(hJob, proc.Handle);
                    
                    // Resume ALL threads in the process to guarantee it wakes up
                    foreach (ProcessThread thread in proc.Threads) {
                        IntPtr hThread = OpenThread(0x0002, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero) {
                            while(ResumeThread(hThread) > 0) { } 
                            CloseHandle(hThread);
                        }
                    }

                    lock (_lock) { _suspendedPids.Remove(pid); _pidToAppName.Remove(pid); _suspendedTitles.Remove(pid); }
                } catch { }
            }
            lock (_lock) { _suspendedApps.Remove(targetName); _lastActiveTimes[targetName] = DateTime.Now; _originalRam.Remove(targetName); }
            if (!skipUpdate) UpdateLists();
        }

        private void CacheProcessWindowTitles(int pid) {
            EnumWindows((hWnd, lParam) => {
                GetWindowThreadProcessId(hWnd, out uint wPid);
                if (wPid == pid) {
                    IntPtr p = Marshal.AllocHGlobal(1024);
                    try {
                        int l = GetWindowText(hWnd, p, 512);
                        if (l > 0) {
                            string t = Marshal.PtrToStringUni(p, l) ?? "";
                            lock (_lock) {
                                if (!_suspendedTitles.TryGetValue(pid, out var list))
                                    _suspendedTitles[pid] = list = new();
                                if (!list.Contains(t)) list.Add(t);
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(p); }
                }
                return true;
            }, IntPtr.Zero);
        }

        private static List<int> GetAllChildIds(int parentId) {
            List<int> c = new(); IntPtr s = CreateToolhelp32Snapshot(2, 0);
            try {
                PROCESSENTRY32 pe = new() { dwSize = (uint) Marshal.SizeOf<PROCESSENTRY32>() };
                if (Process32First(s, ref pe)) do { if (pe.th32ParentProcessID == (uint) parentId) c.Add((int) pe.th32ProcessID); } while (Process32Next(s, ref pe));
            } finally { CloseHandle(s); }
            return c;
        }

        private void ResumeAll() {
            List<string> apps; lock (_lock) apps = _suspendedApps.ToList();
            foreach (var a in apps) PerformWake(a, false, true);
            UpdateLists();
        }

        private void UpdateLists() {
            Process[] all = Process.GetProcesses();
            try {
                var groups = all.Where(p => { try { return !string.IsNullOrEmpty(p.MainWindowTitle); } catch { return false; } })
                    .GroupBy(p => p.ProcessName)
                    .Select(g => {
                        long m = 0; string? f = null;
                        foreach (var p in g) { try { m += p.WorkingSet64; f ??= p.MainModule?.FileName; } catch { } }
                        long origMem = 0;
                        DateTime lastActive = DateTime.MinValue;
                        lock (_lock) { 
                            _originalRam.TryGetValue(g.Key, out origMem); 
                            _lastActiveTimes.TryGetValue(g.Key, out lastActive);
                        }
                        return new ProcessData(g.Key, m, f, origMem, lastActive);
                    })
                    .Where(x => !_safeList.Contains(x.Name) && !x.Name.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Memory).ToList();
                lock (_lock) { UpdateListBox(LbApps, groups.Where(x => !_suspendedApps.Contains(x.Name))); UpdateListBox(LbSuspended, groups.Where(x => _suspendedApps.Contains(x.Name))); }
            } finally { foreach (var p in all) p.Dispose(); }
        }

        private void UpdateListBox(ListBox lb, IEnumerable<ProcessData> items) {
            var selectedNames = lb.SelectedItems.Cast<ProcessItem>().Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int topIndex = lb.Items.Count > 0 ? lb.TopIndex : 0;
            
            lb.BeginUpdate(); lb.Items.Clear();
            foreach (var x in items) {
                if (!_iconCache.ContainsKey(x.Name) && x.FilePath != null) try { _iconCache[x.Name] = Icon.ExtractAssociatedIcon(x.FilePath); } catch { _iconCache[x.Name] = null; }
                lb.Items.Add(new ProcessItem { Name = x.Name, Memory = x.Memory, OriginalMemory = x.OriginalMemory, LastActive = x.LastActive, Icon = _iconCache.GetValueOrDefault(x.Name) });
            }
            
            for (int i = 0; i < lb.Items.Count; i++) {
                if (selectedNames.Contains(((ProcessItem)lb.Items[i]).Name)) {
                    lb.SetSelected(i, true);
                }
            }
            
            if (topIndex >= 0 && topIndex < lb.Items.Count) lb.TopIndex = topIndex;
            lb.EndUpdate();
        }

        private void Lb_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0) return;
            var lb = (ListBox) sender; var item = (ProcessItem) lb.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            e.Graphics.FillRectangle(sel ? Brushes.DodgerBlue : new SolidBrush(lb.BackColor), e.Bounds);
            if (item.Icon != null) e.Graphics.DrawIcon(new Icon(item.Icon, 32, 32), e.Bounds.X + 4, e.Bounds.Y + 4);
            e.Graphics.DrawString(item.Name, e.Font!, Brushes.White, e.Bounds.X + 45, e.Bounds.Y + 10);
            
            string memStr = item.OriginalMemory > 0 && lb == LbSuspended ? $"{item.Memory / 1024 / 1024}MB / {item.OriginalMemory / 1024 / 1024}MB" : $"{item.Memory / 1024 / 1024}MB";
            
            if (lb == LbApps && _chkEcoPilot != null && _chkEcoPilot.Checked) {
                if (item.LastActive != DateTime.MinValue) {
                    int secondsLeft = (int)(AutoHibernateSeconds - (DateTime.Now - item.LastActive).TotalSeconds);
                    if (secondsLeft < 0) secondsLeft = 0;
                    memStr += $"  ({secondsLeft}s)";
                } else {
                    memStr += $"  ({AutoHibernateSeconds}s)";
                }
            }

            var size = e.Graphics.MeasureString(memStr, e.Font!);
            e.Graphics.DrawString(memStr, e.Font!, Brushes.LightGray, e.Bounds.Right - size.Width - 10, e.Bounds.Y + 10);
        }

        private void StartupSuspendedCheck() {
            var threadMap = new Dictionary<uint, List<uint>>();
            IntPtr hSnap = CreateToolhelp32Snapshot(4, 0); 
            if (hSnap != -1) {
                try {
                    THREADENTRY32 te = new() { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };
                    if (Thread32First(hSnap, ref te)) do { if (!threadMap.TryGetValue(te.th32OwnerProcessID, out var list)) threadMap[te.th32OwnerProcessID] = list = new(); list.Add(te.th32ThreadID); } while (Thread32Next(hSnap, ref te));
                } finally { CloseHandle(hSnap); }
            }
            foreach (var p in Process.GetProcesses()) {
                try {
                    if (_safeList.Contains(p.ProcessName) || p.ProcessName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (threadMap.TryGetValue((uint)p.Id, out var threads) && threads.Count > 0) {
                        bool allSuspended = true;
                        foreach (uint tid in threads) {
                            IntPtr hThread = OpenThread(2, false, tid);
                            if (hThread != IntPtr.Zero) { try { uint prevCount = SuspendThread(hThread); ResumeThread(hThread); if (prevCount == 0) { allSuspended = false; break; } } finally { CloseHandle(hThread); } } else { allSuspended = false; break; }
                        }
                        if (allSuspended) { lock (_lock) { _suspendedApps.Add(p.ProcessName); _suspendedPids.Add(p.Id); _pidToAppName[p.Id] = p.ProcessName; CacheProcessWindowTitles(p.Id); } }
                    }
                } catch { }
            }
        }

        private void GraphTimer_Tick(object sender, EventArgs e) {
            UpdateLists();
            if (_chkEcoPilot != null && _chkEcoPilot.Checked) {
                var now = DateTime.Now; List<string> toHib = new();
                lock (_lock) {
                    foreach (var item in LbApps.Items) if (item is ProcessItem pi) {
                        if (_lastActiveTimes.TryGetValue(pi.Name, out var last)) { if ((now - last).TotalSeconds >= AutoHibernateSeconds) toHib.Add(pi.Name); } else _lastActiveTimes[pi.Name] = now;
                    }
                }
                foreach (var n in toHib) HibernateProcess(n);
            }
            MEMORYSTATUSEX ms = MEMORYSTATUSEX.Create();
            if (GlobalMemoryStatusEx(ref ms)) _memHistory.Add(0); 
            if (_memHistory.Count > MaxHistory) _memHistory.RemoveAt(0);
            PicGraph.Invalidate();
        }

        private void PicGraph_Paint(object sender, PaintEventArgs e) {
            if (_memHistory.Count < 2) return;
            using var pen = new Pen(Color.LimeGreen, 2);
            float w = PicGraph.Width, h = PicGraph.Height, s = w / (MaxHistory - 1);
            for (int i = 0; i < _memHistory.Count - 1; i++) e.Graphics.DrawLine(pen, i * s, h - 20, (i + 1) * s, h - 20);
        }

        private static IntPtr CreateGhostJob() {
            IntPtr j = CreateJobObject(IntPtr.Zero, "Ghost_" + Guid.NewGuid());
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION l = new();
            l.BasicLimitInformation.LimitFlags = 0x00000100; l.ProcessMemoryLimit = 512 * 1024 * 1024;
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(l)); Marshal.StructureToPtr(l, p, false);
            SetInformationJobObject(j, 9, p, (uint) Marshal.SizeOf(l)); Marshal.FreeHGlobal(p);
            return j;
        }

        private void Form1_Resize(object? sender, EventArgs e) { if (WindowState == FormWindowState.Minimized) Hide(); }
        private void TrayIcon_DoubleClick(object? sender, EventArgs e) => RestoreWindow();
        private void MenuRestore_Click(object? sender, EventArgs e) => RestoreWindow();
        private void MenuQuit_Click(object? sender, EventArgs e) => Application.Exit();
        private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        protected override void OnFormClosing(FormClosingEventArgs e) { if (_hHook != IntPtr.Zero) UnhookWinEvent(_hHook); ResumeAll(); base.OnFormClosing(e); }
    }
}
