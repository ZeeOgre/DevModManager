using System;

namespace DevModManager.Core.Models
{
    public class ModDatum
    {
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }

        public bool IsActive { get; set; }
        public bool OnCreations { get; set; }
        public bool OnNexus { get; set; }
        public bool HasGitRepo { get; set; }
    }
}
