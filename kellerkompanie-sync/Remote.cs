using System.Collections.Generic;

namespace kellerkompanie_sync
{
    public class RemoteIndex
    {
        public Dictionary<Uuid, RemoteAddon> FilesIndex { get; set; }
        public List<AddonGroup> AddonGroups { get; set; }
    }

    public class RemoteAddon
    {
        public string Name { get; set; }

        public Uuid Uuid { get; set; }

        public string Version { get; set; }

        public Dictionary<FilePath, RemoteAddonFile> Files { get; set; }

        public override string ToString()
        {
            return string.Format("{{{0}, {1}}}", Name, Uuid);
        }
    }

    public class RemoteAddonFile
    {
        public FilePath Path { get; set; }

        public long Size { get; set; }

        public string Hash { get; set; }

        public override string ToString()
        {
            return string.Format("{{{0}}}", Path);
        }
    }
}
