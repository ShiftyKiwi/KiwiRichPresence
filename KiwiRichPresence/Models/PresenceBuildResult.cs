using DiscordRPC;

namespace KiwiRichPresence.Models
{
    internal sealed class PresenceBuildResult
    {
        public DiscordRPC.RichPresence? Presence { get; init; }
        public bool ShouldClearPresence { get; init; }
        public PresencePreviewSnapshot Preview { get; init; } = new();
    }
}
