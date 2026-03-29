using Dalamud.RichPresence.Models;

namespace Dalamud.RichPresence.Configuration
{
    internal class PresenceContextOverride
    {
        public bool Enabled = false;
        public PresenceLocationPrivacyMode LocationPrivacyMode = PresenceLocationPrivacyMode.Inherit;

        public bool UseDetailsTemplate = false;
        public string DetailsTemplate = string.Empty;

        public bool UseStateTemplate = false;
        public string StateTemplate = string.Empty;

        public PresenceImageOverrideMode LargeImageMode = PresenceImageOverrideMode.Inherit;
        public string LargeImageUrl = string.Empty;
        public bool UseLargeImageTextTemplate = false;
        public string LargeImageTextTemplate = string.Empty;

        public PresenceImageOverrideMode SmallImageMode = PresenceImageOverrideMode.Inherit;
        public string SmallImageUrl = string.Empty;
        public bool UseSmallImageTextTemplate = false;
        public string SmallImageTextTemplate = string.Empty;

        public bool HideParty = false;
    }
}
