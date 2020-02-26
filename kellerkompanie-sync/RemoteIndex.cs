using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kellerkompanie_sync
{
    public class RemoteIndex
    {
        public RemoteIndex(Dictionary<string, RemoteAddon> map)
        {
            this.map = map;
        }

        public Dictionary<string, RemoteAddon> map {get; set;}
    }
}
