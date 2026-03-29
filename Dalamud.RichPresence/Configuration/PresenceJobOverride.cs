using System.Collections.Generic;

namespace Dalamud.RichPresence.Configuration
{
    internal class PresenceJobOverride : PresenceContextOverride
    {
        public string Label = string.Empty;
        public List<uint> JobIds = [];
    }
}
