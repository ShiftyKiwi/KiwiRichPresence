using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using DiscordRPC;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using Lumina.Extensions;

using Dalamud.RichPresence.Configuration;
using Dalamud.RichPresence.Models;

namespace Dalamud.RichPresence.Managers
{
    internal sealed unsafe class PresenceResolver
    {
        private const int MaxCustomImageUrlLength = 256;
        private const int MaxPresenceTextBytes = 128;
        private const string DefaultLargeImageKey = "li_1";
        private const string DefaultSmallImageKey = "class_0";

        private static readonly string[] TemplateTokenKeys =
        [
            "context", "details", "state", "name", "fc", "world", "home_world", "data_center",
            "zone", "region", "location", "job", "job_short", "level", "status",
            "queue_position", "queue_eta", "party_size", "party_max",
        ];

        private readonly Dictionary<uint, TerritoryType> territoriesById;
        private readonly Lumina.Excel.ExcelSheet<TerritoryType> territoryTypeSheet;
        private readonly Dictionary<uint, ContentFinderCondition> contentFinderConditionsByTerritoryId;
        private readonly Lumina.Excel.ExcelSheet<OnlineStatus> onlineStatusSheetEn;
        private readonly uint afkOnlineStatusRowId;

        public PresenceResolver(
            List<TerritoryType> territories,
            Lumina.Excel.ExcelSheet<TerritoryType> territoryTypeSheet,
            Lumina.Excel.ExcelSheet<ContentFinderCondition> contentFinderConditionSheet)
        {
            this.territoriesById = territories
                .Where(territory => territory.RowId != 0)
                .GroupBy(territory => territory.RowId)
                .ToDictionary(group => group.Key, group => group.First());
            this.territoryTypeSheet = territoryTypeSheet;
            this.contentFinderConditionsByTerritoryId = contentFinderConditionSheet
                .Where(cfc => cfc.RowId != 0 && cfc.TerritoryType.RowId != 0)
                .GroupBy(cfc => cfc.TerritoryType.RowId)
                .ToDictionary(group => group.Key, group => group.First());
            this.onlineStatusSheetEn = RichPresencePlugin.DataManager.GetExcelSheet<OnlineStatus>(ClientLanguage.English);
            this.afkOnlineStatusRowId = this.onlineStatusSheetEn
                .FirstOrDefault(status => string.Equals(status.Name.ExtractText(), "Away from Keyboard", StringComparison.Ordinal))
                .RowId;
        }

        public static string GetTemplateTokenHelpText()
        {
            return "Template tokens: " + string.Join(" ", TemplateTokenKeys.Select(token => $"{{{token}}}"));
        }

        public PresenceBuildResult Build(RichPresenceConfig config, DateTime startTime)
        {
            var timestamps = config.ShowStartTime ? new Timestamps(startTime) : null;
            var localPlayer = RichPresencePlugin.ObjectTable.LocalPlayer;
            if (localPlayer is null)
            {
                return BuildLoggedOut(config, timestamps);
            }

            return BuildLoggedIn(config, localPlayer, timestamps);
        }

        private PresenceBuildResult BuildLoggedOut(RichPresenceConfig config, Timestamps? timestamps)
        {
            if (config.ShowLoginQueuePosition && RichPresencePlugin.IpcManager.IsInLoginQueue())
            {
                var queuePosition = RichPresencePlugin.IpcManager.GetQueuePosition();
                if (queuePosition >= 0)
                {
                    var queueEta = RichPresencePlugin.IpcManager.GetQueueEstimate()?.TotalSeconds >= 1d
                        ? string.Format(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceQueueEstimate", LocalizationLanguage.Client), RichPresencePlugin.IpcManager.GetQueueEstimate())
                        : string.Empty;
                    var queueDetails = string.Format(
                        RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceInLoginQueue", LocalizationLanguage.Client),
                        queuePosition);
                    var queueData = new PresenceContextData
                    {
                        ContextType = PresenceContextType.Queue,
                        ContextLabel = "Login Queue",
                        GenericLocation = "Login Queue",
                        Location = "Login Queue",
                        QueuePosition = queuePosition,
                        QueueEta = queueEta,
                        AutoDetails = queueDetails,
                        AutoState = queueEta,
                        AutoLargeImageKey = DefaultLargeImageKey,
                        AutoLargeImageText = "Login Queue",
                        AutoSmallImageKey = DefaultSmallImageKey,
                        AutoSmallImageText = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceOnline", LocalizationLanguage.Client),
                    };

                    return Finalize(config, queueData, timestamps, false);
                }
            }

            var menuText = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceInMenus", LocalizationLanguage.Client);
            var menuData = new PresenceContextData
            {
                ContextType = PresenceContextType.Menu,
                ContextLabel = "Menus",
                GenericLocation = menuText,
                Location = menuText,
                AutoDetails = menuText,
                AutoState = string.Empty,
                AutoLargeImageKey = DefaultLargeImageKey,
                AutoLargeImageText = menuText,
                AutoSmallImageKey = DefaultSmallImageKey,
                AutoSmallImageText = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceOnline", LocalizationLanguage.Client),
            };

            return Finalize(config, menuData, timestamps, false);
        }

        private PresenceBuildResult BuildLoggedIn(RichPresenceConfig config, Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer, Timestamps? timestamps)
        {
            var currentTerritoryId = RichPresencePlugin.ClientState.TerritoryType;
            var isHousing = HousingManager.Instance() is not null && HousingManager.Instance()->IsInside();
            var resolvedTerritoryId = isHousing
                ? this.territoryTypeSheet.GetRow(HousingManager.GetOriginalHouseTerritoryTypeId()).RowId
                : currentTerritoryId;
            this.contentFinderConditionsByTerritoryId.TryGetValue(currentTerritoryId, out var cfcTerri);
            var isDuty = cfcTerri.RowId != 0;

            var onlineStatusEn = this.onlineStatusSheetEn.GetRow(localPlayer.OnlineStatus.RowId).Name.ExtractText() ?? string.Empty;
            var isAfk = this.afkOnlineStatusRowId != 0
                ? localPlayer.OnlineStatus.RowId == this.afkOnlineStatusRowId
                : onlineStatusEn.Contains("Away from Keyboard", StringComparison.Ordinal);
            var contextType = DetermineContextType(config, isAfk, isHousing, isDuty);
            var contextOverride = GetContextOverride(config, contextType);
            var privacyMode = ResolvePrivacyMode(config, contextOverride);

            var territoryName = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceTheSource", LocalizationLanguage.Client);
            var territoryRegion = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceVoid", LocalizationLanguage.Client);
            var autoLargeImageKey = DefaultLargeImageKey;
            if (resolvedTerritoryId != 0)
            {
                if (this.territoriesById.TryGetValue(resolvedTerritoryId, out var territory))
                {
                    territoryName = territory.PlaceName.Value.Name.ExtractText()
                        ?? RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceUnknown", LocalizationLanguage.Client);
                    territoryRegion = territory.PlaceNameRegion.Value.Name.ExtractText()
                        ?? RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceUnknown", LocalizationLanguage.Client);
                    if (privacyMode == PresenceLocationPrivacyMode.Exact)
                    {
                        autoLargeImageKey = $"li_{territory.LoadingImage.RowId}";
                    }
                }
            }

            var genericLocation = GetGenericLocationLabel(contextType, onlineStatusEn);
            var location = ResolveLocationText(contextType, privacyMode, territoryName, territoryRegion, genericLocation);
            var secondaryLocation = ResolveSecondaryLocationText(privacyMode, territoryName, territoryRegion, genericLocation);

            var details = localPlayer.Name.ToString();
            var state = localPlayer.CurrentWorld.Value.Name.ExtractText();
            if (config.ShowDataCenter)
            {
                state = $"{state} ({localPlayer.CurrentWorld.Value.DataCenter.Value.Name.ExtractText()})";
            }

            if (config.ShowName)
            {
                if (config.ShowFreeCompany && localPlayer.CurrentWorld.RowId == localPlayer.HomeWorld.RowId)
                {
                    var fcTag = localPlayer.CompanyTag.TextValue;
                    details = string.IsNullOrEmpty(fcTag) ? details : $"{details} \u00AB{fcTag}\u00BB";
                }

                if (config.ShowWorld && localPlayer.CurrentWorld.RowId != localPlayer.HomeWorld.RowId)
                {
                    details = $"{details} \u2740 {localPlayer.HomeWorld.Value.Name}";
                }
                else if (config.AlwaysShowHomeWorld)
                {
                    details = $"{details} \u2740 {localPlayer.HomeWorld.Value.Name}";
                }
            }
            else
            {
                details = location;
            }

            if (!config.ShowWorld)
            {
                state = config.ShowName ? location : secondaryLocation;
            }

            var smallImageKey = DefaultSmallImageKey;
            var smallImageText = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceOnline", LocalizationLanguage.Client);
            if (config.ShowJob)
            {
                smallImageKey = $"class_{localPlayer.ClassJob.RowId}";
                smallImageText = config.AbbreviateJob
                    ? localPlayer.ClassJob.Value.Abbreviation.ExtractText()
                    : RichPresencePlugin.LocalizationManager.TitleCase(localPlayer.ClassJob.Value.Name.ExtractText());
                if (config.ShowLevel)
                {
                    smallImageText = $"{smallImageText} {string.Format(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceLevel", LocalizationLanguage.Client), localPlayer.Level)}";
                }
            }

            var partySize = 0;
            var partyMax = 0;
            var partyId = string.Empty;
            if (config.ShowParty)
            {
                partyId = TryResolvePartyData(cfcTerri, out partySize, out partyMax);
            }

            if (isDuty)
            {
                state = RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceInADuty", LocalizationLanguage.Client);
            }

            if (config.ShowAfk && isAfk)
            {
                state = onlineStatusEn;
                smallImageKey = "away";
            }

            var data = new PresenceContextData
            {
                ContextType = contextType,
                ContextLabel = GetContextLabel(contextType),
                GenericLocation = genericLocation,
                Name = localPlayer.Name.ToString(),
                FreeCompanyTag = localPlayer.CompanyTag.TextValue,
                World = localPlayer.CurrentWorld.Value.Name.ExtractText(),
                HomeWorld = localPlayer.HomeWorld.Value.Name.ExtractText(),
                DataCenter = localPlayer.CurrentWorld.Value.DataCenter.Value.Name.ExtractText(),
                Zone = territoryName,
                Region = territoryRegion,
                Location = location,
                JobId = localPlayer.ClassJob.RowId,
                Job = RichPresencePlugin.LocalizationManager.TitleCase(localPlayer.ClassJob.Value.Name.ExtractText()),
                JobShort = localPlayer.ClassJob.Value.Abbreviation.ExtractText(),
                Level = localPlayer.Level,
                Status = onlineStatusEn,
                PartySize = partySize,
                PartyMax = partyMax,
                PartyId = partyId,
                IsHousing = isHousing,
                IsDuty = isDuty,
                IsAfk = isAfk,
                AutoDetails = details,
                AutoState = state,
                AutoLargeImageKey = autoLargeImageKey,
                AutoLargeImageText = location,
                AutoSmallImageKey = smallImageKey,
                AutoSmallImageText = smallImageText,
            };

            var inCutscene = RichPresencePlugin.Condition[ConditionFlag.OccupiedInCutSceneEvent]
                || RichPresencePlugin.Condition[ConditionFlag.WatchingCutscene]
                || RichPresencePlugin.Condition[ConditionFlag.WatchingCutscene78];
            var clearPresence = (config.HideInCutscene && inCutscene) || (config.HideEntirelyWhenAfk && isAfk);

            return Finalize(config, data, timestamps, clearPresence);
        }

        private PresenceBuildResult Finalize(RichPresenceConfig config, PresenceContextData data, Timestamps? timestamps, bool clearPresence)
        {
            var presence = CreateAutoPresence(data, timestamps);
            ApplyGlobalOverrides(config, data, presence);

            var jobOverride = FindMatchingJobOverride(config, data.JobId);
            if (jobOverride is not null)
            {
                ApplyContextOverride(jobOverride, data, presence);
                if (jobOverride.HideParty)
                {
                    presence.Party = null!;
                }
            }

            var contextOverride = GetContextOverride(config, data.ContextType);
            if (contextOverride.Enabled)
            {
                ApplyContextOverride(contextOverride, data, presence);
                if (contextOverride.HideParty)
                {
                    presence.Party = null!;
                }
            }

            return new PresenceBuildResult
            {
                Presence = clearPresence ? null : presence,
                ShouldClearPresence = clearPresence,
                Preview = new PresencePreviewSnapshot
                {
                    IsAvailable = true,
                    ContextLabel = data.ContextLabel,
                    ActivePresetLabel = BuildActivePresetLabel(jobOverride, contextOverride, data.ContextLabel),
                    Details = presence.Details ?? string.Empty,
                    State = presence.State ?? string.Empty,
                    LargeImageKey = presence.Assets?.LargeImageKey ?? string.Empty,
                    LargeImageText = presence.Assets?.LargeImageText ?? string.Empty,
                    SmallImageKey = presence.Assets?.SmallImageKey ?? string.Empty,
                    SmallImageText = presence.Assets?.SmallImageText ?? string.Empty,
                    PartyText = presence.Party is null ? "None" : $"{presence.Party.Size}/{presence.Party.Max}",
                    ClearsPresence = clearPresence,
                },
            };
        }

        private static string BuildActivePresetLabel(PresenceJobOverride? jobOverride, PresenceContextOverride contextOverride, string contextLabel)
        {
            var activeLabels = new List<string> { "Global overrides" };
            if (jobOverride is not null)
            {
                activeLabels.Add(string.IsNullOrWhiteSpace(jobOverride.Label) ? "Job preset" : jobOverride.Label.Trim());
            }

            if (contextOverride.Enabled)
            {
                activeLabels.Add($"{contextLabel} preset");
            }

            return string.Join(" + ", activeLabels);
        }

        private static DiscordRPC.RichPresence CreateAutoPresence(PresenceContextData data, Timestamps? timestamps)
        {
            var presence = new DiscordRPC.RichPresence
            {
                Details = data.AutoDetails,
                State = data.AutoState,
                Assets = new Assets
                {
                    LargeImageKey = data.AutoLargeImageKey,
                    LargeImageText = data.AutoLargeImageText,
                    SmallImageKey = data.AutoSmallImageKey,
                    SmallImageText = data.AutoSmallImageText,
                },
                Timestamps = timestamps,
            };

            if (data.PartySize > 0 && data.PartyMax > 0 && !string.IsNullOrWhiteSpace(data.PartyId))
            {
                presence.Party = new Party
                {
                    Size = data.PartySize,
                    Max = data.PartyMax,
                    ID = data.PartyId,
                };
            }

            return presence;
        }

        private static void ApplyGlobalOverrides(RichPresenceConfig config, PresenceContextData data, DiscordRPC.RichPresence presence)
        {
            presence.Details = ResolveTemplateOverride(config.UseCustomDetailsText, config.CustomDetailsText, data, presence, presence.Details ?? string.Empty);
            presence.State = ResolveTemplateOverride(config.UseCustomStateText, config.CustomStateText, data, presence, presence.State ?? string.Empty);

            ApplyImageMode(config.HideLargeImage ? PresenceImageOverrideMode.None : config.UseCustomLargeImage ? PresenceImageOverrideMode.CustomUrl : PresenceImageOverrideMode.Auto, config.CustomLargeImageUrl, data.AutoLargeImageKey, presence.Assets, true);
            presence.Assets.LargeImageText = ResolveTemplateOverride(config.UseCustomLargeImageText, config.CustomLargeImageTextTemplate, data, presence, presence.Assets.LargeImageText ?? string.Empty);

            ApplyImageMode(config.HideSmallImage ? PresenceImageOverrideMode.None : config.UseCustomSmallImage ? PresenceImageOverrideMode.CustomUrl : PresenceImageOverrideMode.Auto, config.CustomSmallImageUrl, data.AutoSmallImageKey, presence.Assets, false);
            presence.Assets.SmallImageText = ResolveTemplateOverride(config.UseCustomSmallImageText, config.CustomSmallImageTextTemplate, data, presence, presence.Assets.SmallImageText ?? string.Empty);
        }

        private static void ApplyContextOverride(PresenceContextOverride contextOverride, PresenceContextData data, DiscordRPC.RichPresence presence)
        {
            presence.Details = ResolveTemplateOverride(contextOverride.UseDetailsTemplate, contextOverride.DetailsTemplate, data, presence, presence.Details ?? string.Empty);
            presence.State = ResolveTemplateOverride(contextOverride.UseStateTemplate, contextOverride.StateTemplate, data, presence, presence.State ?? string.Empty);

            ApplyImageMode(contextOverride.LargeImageMode, contextOverride.LargeImageUrl, data.AutoLargeImageKey, presence.Assets, true);
            presence.Assets.LargeImageText = ResolveTemplateOverride(contextOverride.UseLargeImageTextTemplate, contextOverride.LargeImageTextTemplate, data, presence, presence.Assets.LargeImageText ?? string.Empty);

            ApplyImageMode(contextOverride.SmallImageMode, contextOverride.SmallImageUrl, data.AutoSmallImageKey, presence.Assets, false);
            presence.Assets.SmallImageText = ResolveTemplateOverride(contextOverride.UseSmallImageTextTemplate, contextOverride.SmallImageTextTemplate, data, presence, presence.Assets.SmallImageText ?? string.Empty);
        }

        private static void ApplyImageMode(PresenceImageOverrideMode imageMode, string customImageUrl, string autoImageKey, Assets assets, bool isLargeImage)
        {
            switch (imageMode)
            {
                case PresenceImageOverrideMode.Inherit:
                    return;
                case PresenceImageOverrideMode.None:
                    SetImageKey(assets, isLargeImage, null);
                    return;
                case PresenceImageOverrideMode.CustomUrl:
                    var validatedCustomImageUrl = ValidateCustomImageUrl(customImageUrl);
                    if (!string.IsNullOrWhiteSpace(validatedCustomImageUrl))
                    {
                        SetImageKey(assets, isLargeImage, validatedCustomImageUrl);
                    }
                    return;
                case PresenceImageOverrideMode.Auto:
                default:
                    SetImageKey(assets, isLargeImage, autoImageKey);
                    return;
            }
        }

        private static void SetImageKey(Assets assets, bool isLargeImage, string? imageKey)
        {
            if (isLargeImage)
            {
                assets.LargeImageKey = imageKey ?? null!;
                if (string.IsNullOrWhiteSpace(imageKey))
                {
                    assets.LargeImageText = null!;
                }
            }
            else
            {
                assets.SmallImageKey = imageKey ?? null!;
                if (string.IsNullOrWhiteSpace(imageKey))
                {
                    assets.SmallImageText = null!;
                }
            }
        }

        private static string ResolveTemplateOverride(bool isTemplateEnabled, string template, PresenceContextData data, DiscordRPC.RichPresence presence, string fallbackValue)
        {
            if (!isTemplateEnabled)
            {
                return fallbackValue;
            }

            var renderedTemplate = RenderTemplate(template, data, presence);
            return string.IsNullOrWhiteSpace(renderedTemplate) ? fallbackValue : renderedTemplate;
        }

        private static string RenderTemplate(string template, PresenceContextData data, DiscordRPC.RichPresence presence)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var renderedTemplate = template;
            foreach (var token in BuildTemplateTokens(data, presence))
            {
                renderedTemplate = renderedTemplate.Replace($"{{{token.Key}}}", token.Value, StringComparison.Ordinal);
            }

            var trimmedTemplate = renderedTemplate.Trim();
            return Encoding.UTF8.GetByteCount(trimmedTemplate) <= MaxPresenceTextBytes ? trimmedTemplate : string.Empty;
        }

        private static Dictionary<string, string> BuildTemplateTokens(PresenceContextData data, DiscordRPC.RichPresence presence)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["context"] = data.ContextLabel,
                ["details"] = presence.Details ?? string.Empty,
                ["state"] = presence.State ?? string.Empty,
                ["name"] = data.Name,
                ["fc"] = data.FreeCompanyTag,
                ["world"] = data.World,
                ["home_world"] = data.HomeWorld,
                ["data_center"] = data.DataCenter,
                ["zone"] = data.Zone,
                ["region"] = data.Region,
                ["location"] = data.Location,
                ["job"] = data.Job,
                ["job_short"] = data.JobShort,
                ["level"] = data.Level > 0 ? data.Level.ToString() : string.Empty,
                ["status"] = data.Status,
                ["queue_position"] = data.QueuePosition >= 0 ? data.QueuePosition.ToString() : string.Empty,
                ["queue_eta"] = data.QueueEta,
                ["party_size"] = data.PartySize > 0 ? data.PartySize.ToString() : string.Empty,
                ["party_max"] = data.PartyMax > 0 ? data.PartyMax.ToString() : string.Empty,
            };
        }

        private static string? ValidateCustomImageUrl(string customImageUrl)
        {
            var trimmedCustomImageUrl = customImageUrl.Trim();
            if (trimmedCustomImageUrl.Length is <= 0 or > MaxCustomImageUrlLength)
            {
                return null;
            }

            if (!Uri.TryCreate(trimmedCustomImageUrl, UriKind.Absolute, out var parsedUrl))
            {
                return null;
            }

            return parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps
                ? trimmedCustomImageUrl
                : null;
        }

        private static PresenceContextType DetermineContextType(RichPresenceConfig config, bool isAfk, bool isHousing, bool isDuty)
        {
            if (config.ShowAfk && isAfk)
            {
                return PresenceContextType.Afk;
            }

            if (isDuty)
            {
                return PresenceContextType.Duty;
            }

            if (isHousing)
            {
                return PresenceContextType.Housing;
            }

            return PresenceContextType.OpenWorld;
        }

        private static PresenceContextOverride GetContextOverride(RichPresenceConfig config, PresenceContextType contextType)
        {
            return contextType switch
            {
                PresenceContextType.Menu => config.MenuOverride,
                PresenceContextType.Queue => config.QueueOverride,
                PresenceContextType.OpenWorld => config.OpenWorldOverride,
                PresenceContextType.Housing => config.HousingOverride,
                PresenceContextType.Duty => config.DutyOverride,
                PresenceContextType.Afk => config.AfkOverride,
                _ => config.OpenWorldOverride,
            };
        }

        private static PresenceJobOverride? FindMatchingJobOverride(RichPresenceConfig config, uint jobId)
        {
            if (jobId == 0 || config.JobOverrides.Count == 0)
            {
                return null;
            }

            foreach (var jobOverride in config.JobOverrides)
            {
                if (!jobOverride.Enabled || jobOverride.JobIds.Count == 0)
                {
                    continue;
                }

                if (jobOverride.JobIds.Contains(jobId))
                {
                    return jobOverride;
                }
            }

            return null;
        }

        private static PresenceLocationPrivacyMode ResolvePrivacyMode(RichPresenceConfig config, PresenceContextOverride contextOverride)
        {
            return contextOverride.Enabled && contextOverride.LocationPrivacyMode != PresenceLocationPrivacyMode.Inherit
                ? contextOverride.LocationPrivacyMode
                : config.LocationPrivacyMode;
        }

        private static string GetContextLabel(PresenceContextType contextType)
        {
            return contextType switch
            {
                PresenceContextType.Menu => "Menus",
                PresenceContextType.Queue => "Login Queue",
                PresenceContextType.OpenWorld => "Open World",
                PresenceContextType.Housing => "Housing",
                PresenceContextType.Duty => "Duty",
                PresenceContextType.Afk => "AFK",
                _ => "Unknown",
            };
        }

        private static string GetGenericLocationLabel(PresenceContextType contextType, string afkStatus)
        {
            return contextType switch
            {
                PresenceContextType.Menu => RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceInMenus", LocalizationLanguage.Client),
                PresenceContextType.Queue => "Login Queue",
                PresenceContextType.OpenWorld => "Adventuring",
                PresenceContextType.Housing => "Housing District",
                PresenceContextType.Duty => RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceInADuty", LocalizationLanguage.Client),
                PresenceContextType.Afk => string.IsNullOrWhiteSpace(afkStatus) ? "Away from Keyboard" : afkStatus,
                _ => RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceUnknown", LocalizationLanguage.Client),
            };
        }

        private static string ResolveLocationText(PresenceContextType contextType, PresenceLocationPrivacyMode privacyMode, string territoryName, string territoryRegion, string genericLocation)
        {
            return privacyMode switch
            {
                PresenceLocationPrivacyMode.Region => string.IsNullOrWhiteSpace(territoryRegion) ? territoryName : territoryRegion,
                PresenceLocationPrivacyMode.Generic => genericLocation,
                _ => territoryName,
            };
        }

        private static string ResolveSecondaryLocationText(PresenceLocationPrivacyMode privacyMode, string territoryName, string territoryRegion, string genericLocation)
        {
            return privacyMode switch
            {
                PresenceLocationPrivacyMode.Exact => string.IsNullOrWhiteSpace(territoryRegion) ? territoryName : territoryRegion,
                PresenceLocationPrivacyMode.Region => genericLocation,
                PresenceLocationPrivacyMode.Generic => genericLocation,
                _ => string.IsNullOrWhiteSpace(territoryRegion) ? territoryName : territoryRegion,
            };
        }

        private static string TryResolvePartyData(ContentFinderCondition cfcTerri, out int partySize, out int partyMax)
        {
            partySize = 0;
            partyMax = 0;

            if (RichPresencePlugin.PartyList.Length > 0 && RichPresencePlugin.PartyList.PartyId != 0)
            {
                partySize = RichPresencePlugin.PartyList.Length;
                partyMax = cfcTerri.RowId != 0 && cfcTerri.ContentType.RowId == 2 ? 4 : 8;
                if (partySize > partyMax)
                {
                    partyMax = partySize;
                }

                return RichPresencePlugin.GetStringSha256Hash(RichPresencePlugin.PartyList.PartyId.ToString());
            }

            var ipCrossRealm = InfoProxyCrossRealm.Instance();
            if (!ipCrossRealm->IsInCrossRealmParty)
            {
                return string.Empty;
            }

            var numMembers = InfoProxyCrossRealm.GetGroupMemberCount(ipCrossRealm->LocalPlayerGroupIndex);
            if (numMembers <= 0)
            {
                return string.Empty;
            }

            var memberAry = new CrossRealmMember[numMembers];
            for (var i = 0u; i < numMembers; i++)
            {
                memberAry[i] = *InfoProxyCrossRealm.GetGroupMember(i, ipCrossRealm->LocalPlayerGroupIndex);
            }

            partySize = numMembers;
            partyMax = 8;
            var lowestCid = memberAry.OrderBy(x => x.ContentId).Select(x => x.ContentId).First();
            return RichPresencePlugin.GetStringSha256Hash(lowestCid.ToString());
        }
    }
}
