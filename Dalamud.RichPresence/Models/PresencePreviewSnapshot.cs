namespace Dalamud.RichPresence.Models
{
    internal sealed class PresencePreviewSnapshot
    {
        public bool IsAvailable { get; init; }
        public string ContextLabel { get; init; } = string.Empty;
        public string ActivePresetLabel { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string LargeImageKey { get; init; } = string.Empty;
        public string LargeImageText { get; init; } = string.Empty;
        public string SmallImageKey { get; init; } = string.Empty;
        public string SmallImageText { get; init; } = string.Empty;
        public string PartyText { get; init; } = string.Empty;
        public bool ClearsPresence { get; init; }
    }
}
