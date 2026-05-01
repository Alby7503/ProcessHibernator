using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ProcessHibernator.Models;
using ProcessHibernator.Logic;
using ProcessHibernator.Native;

namespace ProcessHibernator {
    public partial class MainForm : Form {
        private readonly ProcessManager _processManager = new();
        private readonly InteractionEngine _interactionEngine;
        private readonly List<long> _memHistory = [];
        private const int MaxHistory = 50;
        private const double AutoHibernateSeconds = 10;
        private readonly Dictionary<string, Icon?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _safeList = new(StringComparer.OrdinalIgnoreCase) { 
            "explorer", "dwm", "taskmgr", "logonui", "devenv", "ProcessHibernator", "discord", "cmd" 
        };
        private CheckBox? _chkEcoPilot;

        public MainForm() {
            InitializeComponent();
            SetupEcoPilotUI();
            TrayIcon.Icon = this.Icon;

            _processManager.StartupSuspendedCheck(_safeList);
            _interactionEngine = new InteractionEngine(_processManager);

            AppDomain.CurrentDomain.ProcessExit += (s, e) => _processManager.ResumeAll();
        }

        private void SetupEcoPilotUI() {
            _chkEcoPilot = new CheckBox { 
                Text = "Eco-Pilot (Auto)", 
                ForeColor = Color.LimeGreen, 
                AutoSize = true, 
                Location = new Point(408, 385), 
                FlatStyle = FlatStyle.Flat 
            };
            this.Controls.Add(_chkEcoPilot);
            LbApps.DrawMode = DrawMode.OwnerDrawFixed; 
            LbSuspended.DrawMode = DrawMode.OwnerDrawFixed;
            LbApps.SelectionMode = SelectionMode.MultiExtended; 
            LbSuspended.SelectionMode = SelectionMode.MultiExtended;
        }

        private void BtnHibernate_Click(object? sender, EventArgs e) { 
            foreach (ProcessItem item in LbApps.SelectedItems.Cast<ProcessItem>().ToList()) 
                _processManager.HibernateProcess(item.Name); 
        }

        private void BtnWake_Click(object? sender, EventArgs e) { 
            foreach (ProcessItem item in LbSuspended.SelectedItems.Cast<ProcessItem>().ToList()) 
                _processManager.PerformWake(item.Name, false); 
        }

        private void BtnGhostWake_Click(object? sender, EventArgs e) { 
            foreach (ProcessItem item in LbSuspended.SelectedItems.Cast<ProcessItem>().ToList()) 
                _processManager.PerformWake(item.Name, true); 
        }

        private void BtnKill_Click(object? sender, EventArgs e) {
            var allSelected = LbApps.SelectedItems.Cast<ProcessItem>()
                .Concat(LbSuspended.SelectedItems.Cast<ProcessItem>()).ToList();
            
            if (allSelected.Count > 0 && MessageBox.Show("Kill selected processes?", "Confirm Kill", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                foreach (var pi in allSelected) {
                    foreach (var p in Process.GetProcessesByName(pi.Name)) {
                        try { p.Kill(true); } catch { }
                    }
                    _processManager.PerformWake(pi.Name, false); // Cleanup state
                }
                UpdateLists();
            }
        }

        private void GraphTimer_Tick(object? sender, EventArgs e) {
            UpdateLists();
            
            if (_chkEcoPilot != null && _chkEcoPilot.Checked) {
                var now = DateTime.Now; 
                var toHib = new List<string>();
                
                foreach (var item in LbApps.Items) {
                    if (item is ProcessItem pi) {
                        var last = _processManager.GetLastActive(pi.Name);
                        if (last != DateTime.MinValue) {
                            if ((now - last).TotalSeconds >= AutoHibernateSeconds) 
                                toHib.Add(pi.Name);
                        } else {
                            _processManager.UpdateLastActive(pi.Name);
                        }
                    }
                }
                foreach (var n in toHib) _processManager.HibernateProcess(n);
            }

            var ms = MEMORYSTATUSEX.Create();
            if (NativeMethods.GlobalMemoryStatusEx(ref ms)) {
                _memHistory.Add((long)(ms.ullTotalPhys - ms.ullAvailPhys));
            }
            if (_memHistory.Count > MaxHistory) _memHistory.RemoveAt(0);
            PicGraph.Invalidate();
        }

        private void UpdateLists() {
            var all = Process.GetProcesses();
            try {
                var groups = all.Where(p => { 
                        try { return !string.IsNullOrEmpty(p.MainWindowTitle); } catch { return false; } 
                    })
                    .GroupBy(p => p.ProcessName)
                    .Select(g => {
                        long m = 0; string? f = null;
                        foreach (var p in g) { 
                            try { m += p.WorkingSet64; f ??= p.MainModule?.FileName; } catch { } 
                        }
                        return new ProcessData(g.Key, m, f, _processManager.GetOriginalRam(g.Key), _processManager.GetLastActive(g.Key));
                    })
                    .Where(x => !_safeList.Contains(x.Name) && !x.Name.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Memory).ToList();

                UpdateListBox(LbApps, groups.Where(x => !_processManager.IsSuspended(x.Name)));
                UpdateListBox(LbSuspended, groups.Where(x => _processManager.IsSuspended(x.Name)));
            } finally { 
                foreach (var p in all) p.Dispose(); 
            }
        }

        private void UpdateListBox(ListBox lb, IEnumerable<ProcessData> items) {
            var selectedNames = lb.SelectedItems.Cast<ProcessItem>().Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int topIndex = lb.Items.Count > 0 ? lb.TopIndex : 0;
            
            lb.BeginUpdate(); 
            lb.Items.Clear();
            foreach (var x in items) {
                if (!_iconCache.ContainsKey(x.Name) && x.FilePath != null) {
                    try { _iconCache[x.Name] = Icon.ExtractAssociatedIcon(x.FilePath); } catch { _iconCache[x.Name] = null; }
                }
                lb.Items.Add(new ProcessItem { 
                    Name = x.Name, 
                    Memory = x.Memory, 
                    OriginalMemory = x.OriginalMemory, 
                    LastActive = x.LastActive, 
                    Icon = _iconCache.GetValueOrDefault(x.Name) 
                });
            }
            
            for (int i = 0; i < lb.Items.Count; i++) {
                if (selectedNames.Contains(((ProcessItem)lb.Items[i]).Name)) {
                    lb.SetSelected(i, true);
                }
            }
            
            if (topIndex >= 0 && topIndex < lb.Items.Count) lb.TopIndex = topIndex;
            lb.EndUpdate();
        }

        private void Lb_DrawItem(object? sender, DrawItemEventArgs e) {
            if (e.Index < 0 || sender is not ListBox lb) return;
            
            var item = (ProcessItem)lb.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            
            e.Graphics.FillRectangle(sel ? Brushes.DodgerBlue : new SolidBrush(lb.BackColor), e.Bounds);
            
            if (item.Icon != null) 
                e.Graphics.DrawIcon(new Icon(item.Icon, 32, 32), e.Bounds.X + 4, e.Bounds.Y + 4);
            
            e.Graphics.DrawString(item.Name, e.Font!, Brushes.White, e.Bounds.X + 45, e.Bounds.Y + 10);
            
            string memStr = item.OriginalMemory > 0 && lb == LbSuspended 
                ? $"{item.Memory / 1024 / 1024}MB / {item.OriginalMemory / 1024 / 1024}MB" 
                : $"{item.Memory / 1024 / 1024}MB";
            
            if (lb == LbApps && _chkEcoPilot != null && _chkEcoPilot.Checked) {
                if (item.LastActive != DateTime.MinValue) {
                    int secondsLeft = (int)(AutoHibernateSeconds - (DateTime.Now - item.LastActive).TotalSeconds);
                    memStr += $"  ({(secondsLeft < 0 ? 0 : secondsLeft)}s)";
                } else {
                    memStr += $"  ({AutoHibernateSeconds}s)";
                }
            }
            
            var size = e.Graphics.MeasureString(memStr, e.Font!); 
            e.Graphics.DrawString(memStr, e.Font!, Brushes.LightGray, e.Bounds.Right - size.Width - 10, e.Bounds.Y + 10);
        }

        private void PicGraph_Paint(object? sender, PaintEventArgs e) {
            if (_memHistory.Count < 2) return;
            
            var ms = MEMORYSTATUSEX.Create(); 
            NativeMethods.GlobalMemoryStatusEx(ref ms);
            long total = (long)ms.ullTotalPhys; 
            if (total == 0) total = 1;
            
            float w = PicGraph.Width, h = PicGraph.Height, s = w / (MaxHistory - 1);
            using var pen = new Pen(Color.LimeGreen, 2);
            for (int i = 0; i < _memHistory.Count - 1; i++) {
                float x1 = i * s, y1 = h - ((float)_memHistory[i] / total * h);
                float x2 = (i + 1) * s, y2 = h - ((float)_memHistory[i + 1] / total * h);
                e.Graphics.DrawLine(pen, x1, y1, x2, y2);
            }

            string memText = $"System RAM: {(double)_memHistory.Last() / 1024 / 1024 / 1024:F1} GB / {(double)total / 1024 / 1024 / 1024:F1} GB";
            e.Graphics.DrawString(memText, this.Font, Brushes.White, 5, 5);
        }

        private void MainForm_Resize(object? sender, EventArgs e) { 
            if (WindowState == FormWindowState.Minimized) Hide(); 
        }

        private void TrayIcon_DoubleClick(object? sender, EventArgs e) => RestoreWindow();
        private void MenuRestore_Click(object? sender, EventArgs e) => RestoreWindow();
        private void MenuQuit_Click(object? sender, EventArgs e) => Application.Exit();

        private void RestoreWindow() { 
            Show(); 
            WindowState = FormWindowState.Normal; 
            Activate(); 
        }

        protected override void OnFormClosing(FormClosingEventArgs e) { 
            _interactionEngine.Dispose();
            _processManager.ResumeAll(); 
            base.OnFormClosing(e); 
        }
    }
}
