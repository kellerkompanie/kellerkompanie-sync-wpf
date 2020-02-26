using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kellerkompanie_sync
{
    public class RemoteAddon
    {
        public string addon_name { get; set; }
        public string addon_uuid { get; set; }
        public string addon_version {get;set;}
        public Dictionary<string, RemoteAddonFile> addon_files { get; set; }
    }
}
