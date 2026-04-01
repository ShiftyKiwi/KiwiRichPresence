using System.Collections.Generic;

namespace KiwiRichPresence.Configuration
{
    internal class PresenceZoneOverride : PresenceContextOverride
    {
        public string Label = string.Empty;
        public List<uint> TerritoryIds = [];
    }
}
