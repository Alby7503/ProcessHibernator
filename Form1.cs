using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;

namespace ProcessHibernator {
    public partial class Form1 : Form {
        // --- WIN32 API SECTION ---
        [LibraryImport("ntdll.dll")]
        private static partial int NtSuspendProcess(IntPtr processHandle);

        [LibraryImport("ntdll.dll")]
        private static partial int NtResumeProcess(IntPtr processHandle);

        [LibraryImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static partial bool EmptyWorkingSet(IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct PROCESSENTRY32 {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            public fixed byte szExeFile[260];
        };

        [LibraryImport("kernel32.dll", SetLastError = true)]
        protected static partial IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static partial bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static partial bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        // --- THE ACTUAL LOGIC ---

        private readonly HashSet<string> _safeList = [with(StringComparer.OrdinalIgnoreCase), "explorer", "dwm", "taskmgr", "logonui", "devenv"];
        private readonly HashSet<string> _suspendedApps = [with(StringComparer.OrdinalIgnoreCase)];
        private readonly List<long> _memHistory = [];
        private const int MaxHistory = 50;
        private readonly Dictionary<string, Icon?> _iconCache = [with(StringComparer.OrdinalIgnoreCase)];

        private record ProcessData(string Name, long Memory, string? FilePath);

        public class ProcessItem {
            public string Name { get; set; } = "";
            public long Memory { get; set; }
            public Icon? Icon { get; set; }
            public override string ToString() => Name;
            
            public override bool Equals(object? obj) => obj is ProcessItem other && Name == other.Name;
            public override int GetHashCode() => Name.GetHashCode();
        }

        public Form1() { 
            InitializeComponent();
            TrayIcon.Icon = this.Icon;
        }

        private void Form1_Resize(object? sender, EventArgs e) {
            if (this.WindowState == FormWindowState.Minimized) {
                this.Hide();
            }
        }

        private void TrayIcon_DoubleClick(object? sender, EventArgs e) {
            RestoreWindow();
        }

        private void MenuRestore_Click(object? sender, EventArgs e) {
            RestoreWindow();
        }

        private void RestoreWindow() {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void MenuQuit_Click(object? sender, EventArgs e) {
            Application.Exit();
        }

        private void BtnHibernate_Click(object sender, EventArgs e) {
            if (LbApps.SelectedItem is not ProcessItem item) return;
            string targetName = item.Name;

            try {
                var roots = Process.GetProcessesByName(targetName);
                if (roots.Length > 0) {
                    foreach (var root in roots) {
                        SuspendAndTrim(root);
                        foreach (int childId in GetAllChildIds(root.Id)) {
                            try {
                                using var child = Process.GetProcessById(childId);
                                SuspendAndTrim(child);
                            } catch { }
                        }
                    }
                    _suspendedApps.Add(targetName);
                    UpdateLists();
                    LblStatus.Text = $"Status: {targetName} frozen.";
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private static void SuspendAndTrim(Process p) {
            try {
                Marshal.ThrowExceptionForHR(NtSuspendProcess(p.Handle));
                EmptyWorkingSet(p.Handle);
            } catch { }
        }

        private static List<int> GetAllChildIds(int parentId) {
            var children = new List<int>();
            IntPtr snapshot = CreateToolhelp32Snapshot(0x00000002, 0);
            if (snapshot == (IntPtr)(-1)) return children;
            try {
                PROCESSENTRY32 pe = new() { dwSize = (uint) Marshal.SizeOf<PROCESSENTRY32>() };
                if (Process32First(snapshot, ref pe)) {
                    do {
                        if (pe.th32ParentProcessID == (uint) parentId) {
                            children.Add((int) pe.th32ProcessID);
                            children.AddRange(GetAllChildIds((int) pe.th32ProcessID));
                        }
                    } while (Process32Next(snapshot, ref pe));
                }
            }
            finally { CloseHandle(snapshot); }
            return children;
        }

        private void BtnWake_Click(object sender, EventArgs e) {
            if (LbSuspended.SelectedItem is not ProcessItem item) return;
            string targetName = item.Name;

            foreach (var proc in Process.GetProcessesByName(targetName)) {
                try { Marshal.ThrowExceptionForHR(NtResumeProcess(proc.Handle)); }
                catch { }
                foreach (int childId in GetAllChildIds(proc.Id)) {
                    try {
                        using var child = Process.GetProcessById(childId);
                        Marshal.ThrowExceptionForHR(NtResumeProcess(child.Handle));
                    } catch { }
                }
            }

            _suspendedApps.Remove(targetName);
            UpdateLists();
            LblStatus.Text = $"Status: {targetName} resumed.";
        }

        private void UpdateLists() {
            var allProcs = Process.GetProcesses();
            var appGroups = allProcs
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .GroupBy(p => p.ProcessName)
                .Select(g => new ProcessData(
                    g.Key,
                    g.Sum(p => { try { return p.WorkingSet64; } catch { return 0; } }),
                    g.First().MainModule?.FileName
                ))
                .Where(x => !x.Name.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase))
                .Where(x => !_safeList.Contains(x.Name))
                .OrderByDescending(x => x.Memory)
                .ToList();

            UpdateListBox(LbApps, appGroups.Where(x => !_suspendedApps.Contains(x.Name)));
            UpdateListBox(LbSuspended, appGroups.Where(x => _suspendedApps.Contains(x.Name)));
        }

        private void UpdateListBox(ListBox lb, IEnumerable<ProcessData> items) {
            var selectedName = (lb.SelectedItem as ProcessItem)?.Name;
            int savedTop = lb.TopIndex;
            
            lb.BeginUpdate();
            lb.Items.Clear();
            foreach (var x in items) {
                if (!_iconCache.ContainsKey(x.Name) && !string.IsNullOrEmpty(x.FilePath)) {
                    try { _iconCache[x.Name] = Icon.ExtractAssociatedIcon(x.FilePath); }
                    catch { _iconCache[x.Name] = null; }
                }
                lb.Items.Add(new ProcessItem { 
                    Name = x.Name, 
                    Memory = x.Memory, 
                    Icon = _iconCache.TryGetValue(x.Name, out Icon? ico) ? ico : null 
                });
            }

            // Restore selection if possible
            if (selectedName != null) {
                for (int i = 0; i < lb.Items.Count; i++) {
                    if ((lb.Items[i] as ProcessItem)?.Name == selectedName) {
                        lb.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Restore scroll position (TopIndex)
            if (savedTop >= 0 && savedTop < lb.Items.Count) {
                lb.TopIndex = savedTop;
            }

            // Only auto-select first item if absolutely nothing is selected and we didn't have a selection before
            if (lb.Items.Count > 0 && lb.SelectedIndex == -1 && selectedName == null) {
                lb.SelectedIndex = 0;
            }

            lb.EndUpdate();
        }

        private void Lb_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0) return;
            var lb = (ListBox)sender;
            var item = (ProcessItem)lb.Items[e.Index];

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            
            // Custom selection background
            if (isSelected) {
                using var selectBrush = new SolidBrush(Color.DodgerBlue);
                e.Graphics.FillRectangle(selectBrush, e.Bounds);
            } else {
                using var backBrush = new SolidBrush(lb.BackColor);
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            // Draw Icon (32x32)
            if (item.Icon != null) {
                try {
                    e.Graphics.DrawIcon(new Icon(item.Icon, 32, 32), e.Bounds.X + 4, e.Bounds.Y + 4);
                } catch { }
            }

            // Draw Name
            string name = item.Name;
            using var textBrush = new SolidBrush(Color.White);
            
            var textSize = e.Graphics.MeasureString(name, e.Font!);
            float textY = e.Bounds.Y + (e.Bounds.Height - textSize.Height) / 2;
            e.Graphics.DrawString(name, e.Font!, textBrush, e.Bounds.X + 45, textY);

            // Draw Memory pill
            string memText = $"{(double)item.Memory / 1024 / 1024:F0}MB";
            SizeF memLabelSize = e.Graphics.MeasureString(memText, e.Font!);
            float memY = e.Bounds.Y + (e.Bounds.Height - memLabelSize.Height) / 2;
            RectangleF memRect = new(e.Bounds.Right - memLabelSize.Width - 12, memY, memLabelSize.Width + 8, memLabelSize.Height);
            
            if (!isSelected) {
                using var pillBrush = new SolidBrush(Color.FromArgb(80, 80, 80));
                e.Graphics.FillRectangle(pillBrush, memRect);
            }
            
            e.Graphics.DrawString(memText, e.Font!, textBrush, e.Bounds.Right - memLabelSize.Width - 8, memY);

            if (isSelected) e.DrawFocusRectangle();
        }

        private void GraphTimer_Tick(object sender, EventArgs e) {
            // Periodic update of lists
            UpdateLists();

            MEMORYSTATUSEX memStatus = new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memStatus)) {
                long usedMem = (long)(memStatus.ullTotalPhys - memStatus.ullAvailPhys);
                _memHistory.Add(usedMem);
            } else {
                _memHistory.Add(0);
            }

            if (_memHistory.Count > MaxHistory) _memHistory.RemoveAt(0);
            PicGraph.Invalidate();
        }

        private void PicGraph_Paint(object sender, PaintEventArgs e) {
            if (_memHistory.Count < 2) return;

            MEMORYSTATUSEX memStatus = new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            GlobalMemoryStatusEx(ref memStatus);
            long total = (long)memStatus.ullTotalPhys;
            if (total == 0) total = 1;

            float w = PicGraph.Width;
            float h = PicGraph.Height;
            float step = w / (MaxHistory - 1);

            using var pen = new Pen(Color.LimeGreen, 2);
            for (int i = 0; i < _memHistory.Count - 1; i++) {
                float x1 = i * step;
                float y1 = h - ((float)_memHistory[i] / total * h);
                float x2 = (i + 1) * step;
                float y2 = h - ((float)_memHistory[i + 1] / total * h);
                e.Graphics.DrawLine(pen, x1, y1, x2, y2);
            }

            string memText = $"System RAM: {(double)_memHistory.Last() / 1024 / 1024 / 1024:F1} GB / {(double)total / 1024 / 1024 / 1024:F1} GB";
            e.Graphics.DrawString(memText, Font, Brushes.White, 5, 5);
        }
    }
}
