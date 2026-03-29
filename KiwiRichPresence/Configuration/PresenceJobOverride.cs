using System.Collections.Generic;

namespace KiwiRichPresence.Configuration
{
    internal class PresenceJobOverride : PresenceContextOverride
    {
        public string Label = string.Empty;
        public List<uint> JobIds = [];
    }
}
