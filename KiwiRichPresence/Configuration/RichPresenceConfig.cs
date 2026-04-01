using System.Collections.Generic;
using Dalamud.Configuration;
using KiwiRichPresence.Models;

namespace KiwiRichPresence.Configuration
{
    internal class RichPresenceConfig : IPluginConfiguration
    {
        public int Version { get; set; } = 3;

        public bool ShowLoginQueuePosition = true;
        public bool ShowName = true;
        public bool ShowFreeCompany = true;
        public bool ShowWorld = true;
        public bool AlwaysShowHomeWorld = false;
        public bool ShowDataCenter = false;

        public bool UseCustomLargeImage = false;
        public string CustomLargeImageUrl = string.Empty;
        public bool HideLargeImage = false;
        public bool UseCustomLargeImageText = false;
        public string CustomLargeImageTextTemplate = string.Empty;

        public bool UseCustomDetailsText = false;
        public string CustomDetailsText = string.Empty;
        public bool UseCustomStateText = false;
        public string CustomStateText = string.Empty;

        public bool HideSmallImage = false;
        public bool UseCustomSmallImage = false;
        public string CustomSmallImageUrl = string.Empty;
        public bool UseCustomSmallImageText = false;
        public string CustomSmallImageTextTemplate = string.Empty;

        public PresenceLocationPrivacyMode LocationPrivacyMode = PresenceLocationPrivacyMode.Exact;

        public PresenceContextOverride MenuOverride = new();
        public PresenceContextOverride QueueOverride = new();
        public PresenceContextOverride OpenWorldOverride = new();
        public PresenceContextOverride HousingOverride = new();
        public PresenceContextOverride DutyOverride = new();
        public PresenceContextOverride AfkOverride = new();
        public List<PresenceJobOverride> JobOverrides = [];
        public List<PresenceZoneOverride> ZoneOverrides = [];

        public bool ShowStartTime = false;
        public bool ResetTimeWhenChangingZones = true;

        public bool ShowJob = true;
        public bool AbbreviateJob = true;
        public bool ShowLevel = true;

        public bool ShowParty = true;

        public bool ShowAfk = true;
        public bool HideEntirelyWhenAfk = false;
        public bool HideInCutscene = false;
        public bool RPCBridgeEnabled = true;
    }
}
