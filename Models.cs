using System;
using System.Drawing;

namespace ProcessHibernator.Models {
    public record ProcessData(string Name, long Memory, string? FilePath, long OriginalMemory, DateTime LastActive);

    public class ProcessItem {
        public string Name { get; set; } = string.Empty;
        public long Memory { get; set; }
        public long OriginalMemory { get; set; }
        public DateTime LastActive { get; set; }
        public Icon? Icon { get; set; }
        public override string ToString() => Name;
    }
}
