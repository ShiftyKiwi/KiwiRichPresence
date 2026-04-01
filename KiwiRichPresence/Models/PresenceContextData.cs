namespace KiwiRichPresence.Models
{
    internal sealed class PresenceContextData
    {
        public PresenceContextType ContextType { get; init; }
        public string ContextLabel { get; init; } = string.Empty;
        public string GenericLocation { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
        public string FreeCompanyTag { get; init; } = string.Empty;
        public string World { get; init; } = string.Empty;
        public string HomeWorld { get; init; } = string.Empty;
        public string DataCenter { get; init; } = string.Empty;
        public uint TerritoryId { get; init; }
        public string Zone { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public uint JobId { get; init; }
        public string Job { get; init; } = string.Empty;
        public string JobShort { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int Level { get; init; }
        public int QueuePosition { get; init; } = -1;
        public string QueueEta { get; init; } = string.Empty;
        public int PartySize { get; init; }
        public int PartyMax { get; init; }
        public string PartyId { get; init; } = string.Empty;
        public bool IsHousing { get; init; }
        public bool IsDuty { get; init; }
        public bool IsAfk { get; init; }

        public string AutoDetails { get; init; } = string.Empty;
        public string AutoState { get; init; } = string.Empty;
        public string AutoLargeImageKey { get; init; } = string.Empty;
        public string AutoLargeImageText { get; init; } = string.Empty;
        public string AutoSmallImageKey { get; init; } = string.Empty;
        public string AutoSmallImageText { get; init; } = string.Empty;
    }
}
