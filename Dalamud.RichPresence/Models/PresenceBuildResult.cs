using DiscordRPC;

namespace Dalamud.RichPresence.Models
{
    internal sealed class PresenceBuildResult
    {
        public DiscordRPC.RichPresence? Presence { get; init; }
        public bool ShouldClearPresence { get; init; }
        public PresencePreviewSnapshot Preview { get; init; } = new();
    }
}
