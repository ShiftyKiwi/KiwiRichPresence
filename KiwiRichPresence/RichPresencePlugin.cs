using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

using KiwiRichPresence.Configuration;
using KiwiRichPresence.Interface;
using KiwiRichPresence.Managers;
using KiwiRichPresence.Models;

namespace KiwiRichPresence
{
    internal unsafe class RichPresencePlugin : IDalamudPlugin, IDisposable
    {
        private static readonly TimeSpan PresencePollInterval = TimeSpan.FromSeconds(2);
        private const int CurrentConfigVersion = 3;

        [PluginService]
        internal static IDalamudPluginInterface DalamudPluginInterface { get; private set; } = null!;

        [PluginService]
        internal static IClientState ClientState { get; private set; } = null!;

        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;

        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        internal static IFramework Framework { get; private set; } = null!;

        [PluginService]
        internal static IObjectTable ObjectTable { get; private set; } = null!;

        [PluginService]
        internal static IPartyList PartyList { get; private set; } = null!;

        [PluginService]
        internal static IPluginLog PluginLog { get; private set; } = null!;

        [PluginService]
        internal static ICondition Condition { get; private set; } = null!;

        internal static LocalizationManager LocalizationManager { get; private set; } = null!;
        internal static DiscordPresenceManager DiscordPresenceManager { get; private set; } = null!;
        internal static IpcManager IpcManager { get; private set; } = null!;
        internal static RichPresenceConfig RichPresenceConfig { get; set; } = null!;

        private static RichPresencePlugin? activeInstance;
        private static RichPresenceConfigWindow RichPresenceConfigWindow = null!;

        private readonly PresenceResolver presenceResolver;
        private PresencePreviewSnapshot cachedPreviewSnapshot = new();
        private RichPresenceConfig? pendingPreviewConfig;
        private bool previewRefreshPending;
        private bool presenceDirty = true;
        private DateTime nextPresencePollAtUtc = DateTime.MinValue;
        private DateTime startTime = DateTime.UtcNow;

        public RichPresencePlugin()
        {
            activeInstance = this;
            RichPresenceConfig = NormalizeConfig(DalamudPluginInterface.GetPluginConfig() as RichPresenceConfig ?? new RichPresenceConfig());

            DiscordPresenceManager = new DiscordPresenceManager();
            LocalizationManager = new LocalizationManager();
            IpcManager = new IpcManager();

            var territories = DataManager.GetExcelSheet<TerritoryType>().ToList();
            var territoryTypeSheet = DataManager.GetExcelSheet<TerritoryType>();
            var contentFinderConditionSheet = DataManager.GetExcelSheet<ContentFinderCondition>();
            this.presenceResolver = new PresenceResolver(territories, territoryTypeSheet, contentFinderConditionSheet);

            RichPresenceConfigWindow = new RichPresenceConfigWindow();
            DalamudPluginInterface.UiBuilder.Draw += RichPresenceConfigWindow.DrawRichPresenceConfigWindow;
            DalamudPluginInterface.UiBuilder.OpenConfigUi += RichPresenceConfigWindow.Open;
            DalamudPluginInterface.UiBuilder.OpenMainUi += RichPresenceConfigWindow.Open;

            Framework.Update += this.UpdateRichPresence;
            ClientState.Login += this.State_Login;
            ClientState.TerritoryChanged += this.State_TerritoryChanged;
            ClientState.Logout += this.State_Logout;

            this.RegisterCommand();
            DalamudPluginInterface.LanguageChanged += this.ReregisterCommand;

            this.MarkPresenceDirty(true);
            RefreshPresenceNow();
        }

        public void Dispose()
        {
            DalamudPluginInterface.LanguageChanged -= this.ReregisterCommand;
            this.UnregisterCommand();

            ClientState.Login -= this.State_Login;
            ClientState.TerritoryChanged -= this.State_TerritoryChanged;
            ClientState.Logout -= this.State_Logout;
            Framework.Update -= this.UpdateRichPresence;

            DalamudPluginInterface.UiBuilder.OpenMainUi -= RichPresenceConfigWindow.Open;
            DalamudPluginInterface.UiBuilder.OpenConfigUi -= RichPresenceConfigWindow.Open;
            DalamudPluginInterface.UiBuilder.Draw -= RichPresenceConfigWindow.DrawRichPresenceConfigWindow;

            LocalizationManager.Dispose();

            DiscordPresenceManager.ClearPresence();
            DiscordPresenceManager.Dispose();

            IpcManager.Dispose();
            activeInstance = null;
        }

        internal static void ApplyCurrentConfiguration(RichPresenceConfig config, bool saveToDisk)
        {
            RichPresenceConfig = NormalizeConfig(config);
            if (saveToDisk)
            {
                DalamudPluginInterface.SavePluginConfig(RichPresenceConfig);
            }

            DiscordPresenceManager.ApplyRuntimeConfig(RichPresenceConfig);
            activeInstance?.MarkPresenceDirty(true);
            RefreshPresenceNow(RichPresenceConfig);
        }

        internal static PresencePreviewSnapshot GetPresencePreviewSnapshot(RichPresenceConfig previewConfig)
        {
            if (activeInstance is null)
            {
                return new PresencePreviewSnapshot();
            }

            if (!Framework.IsInFrameworkUpdateThread)
            {
                activeInstance.QueuePreviewRefresh(CloneConfig(previewConfig));
                return activeInstance.cachedPreviewSnapshot;
            }

            try
            {
                activeInstance.cachedPreviewSnapshot = activeInstance.BuildPresence(previewConfig).Preview;
                return activeInstance.cachedPreviewSnapshot;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not build presence preview.");
                return new PresencePreviewSnapshot();
            }
        }

        internal static string GetTemplateTokenHelpText() => PresenceResolver.GetTemplateTokenHelpText();

        internal static void RefreshPresenceNow(RichPresenceConfig? previewConfig = null)
        {
            if (activeInstance is null)
            {
                return;
            }

            if (!Framework.IsInFrameworkUpdateThread)
            {
                _ = Framework.RunOnFrameworkThread(() => RefreshPresenceNow(previewConfig));
                return;
            }

            try
            {
                var buildResult = activeInstance.BuildPresence(previewConfig ?? RichPresenceConfig);
                activeInstance.cachedPreviewSnapshot = buildResult.Preview;
                activeInstance.OnPresenceRefreshed();
                if (buildResult.ShouldClearPresence)
                {
                    DiscordPresenceManager.ClearPresence();
                    return;
                }

                if (buildResult.Presence is not null)
                {
                    DiscordPresenceManager.SetPresence(buildResult.Presence);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not refresh rich presence.");
            }
        }

        internal static string GetStringSha256Hash(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            using var sha = SHA256.Create();
            var textData = System.Text.Encoding.UTF8.GetBytes(text);
            var hash = sha.ComputeHash(textData);
            return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
        }

        private PresenceBuildResult BuildPresence(RichPresenceConfig config)
        {
            return this.presenceResolver.Build(config, this.startTime);
        }

        private void QueuePreviewRefresh(RichPresenceConfig previewConfig)
        {
            this.pendingPreviewConfig = previewConfig;
            if (this.previewRefreshPending)
            {
                return;
            }

            this.previewRefreshPending = true;
            _ = Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    while (this.pendingPreviewConfig is not null)
                    {
                        var latestPreviewConfig = this.pendingPreviewConfig;
                        this.pendingPreviewConfig = null;
                        this.cachedPreviewSnapshot = this.BuildPresence(latestPreviewConfig).Preview;
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Could not refresh rich presence preview.");
                    this.cachedPreviewSnapshot = new PresencePreviewSnapshot();
                }
                finally
                {
                    this.previewRefreshPending = false;
                    if (this.pendingPreviewConfig is not null)
                    {
                        this.QueuePreviewRefresh(this.pendingPreviewConfig);
                    }
                }
            });
        }

        private void MarkPresenceDirty(bool refreshImmediately)
        {
            this.presenceDirty = true;
            if (refreshImmediately)
            {
                this.nextPresencePollAtUtc = DateTime.MinValue;
            }
        }

        private void OnPresenceRefreshed()
        {
            this.presenceDirty = false;
            this.nextPresencePollAtUtc = DateTime.UtcNow + PresencePollInterval;
        }

        private void UpdateStartTime()
        {
            if (RichPresenceConfig.ResetTimeWhenChangingZones)
            {
                this.startTime = DateTime.UtcNow;
                this.MarkPresenceDirty(true);
            }
        }

        private void State_Login()
        {
            this.UpdateStartTime();
            this.MarkPresenceDirty(true);
        }

        private void State_TerritoryChanged(ushort territoryId)
        {
            this.UpdateStartTime();
            this.MarkPresenceDirty(true);
        }

        private void State_Logout(int type, int code)
        {
            this.UpdateStartTime();
            this.MarkPresenceDirty(true);
        }

        private void ReregisterCommand(string langCode)
        {
            this.UnregisterCommand();
            this.RegisterCommand();
        }

        private void UnregisterCommand()
        {
            CommandManager.RemoveHandler("/prp");
        }

        private void RegisterCommand()
        {
            CommandManager.AddHandler(
                "/prp",
                new CommandInfo((string cmd, string args) => RichPresenceConfigWindow.Toggle())
                {
                    HelpMessage = LocalizationManager.Localize("DalamudRichPresenceOpenConfiguration", LocalizationLanguage.Plugin),
                });
        }

        private void UpdateRichPresence(IFramework framework)
        {
            if (!this.presenceDirty && DateTime.UtcNow < this.nextPresencePollAtUtc)
            {
                return;
            }

            RefreshPresenceNow();
        }

        private static RichPresenceConfig CloneConfig(RichPresenceConfig source)
        {
            return new RichPresenceConfig
            {
                Version = source.Version,
                ShowLoginQueuePosition = source.ShowLoginQueuePosition,
                ShowName = source.ShowName,
                ShowFreeCompany = source.ShowFreeCompany,
                ShowWorld = source.ShowWorld,
                AlwaysShowHomeWorld = source.AlwaysShowHomeWorld,
                ShowDataCenter = source.ShowDataCenter,
                UseCustomLargeImage = source.UseCustomLargeImage,
                CustomLargeImageUrl = source.CustomLargeImageUrl,
                HideLargeImage = source.HideLargeImage,
                UseCustomLargeImageText = source.UseCustomLargeImageText,
                CustomLargeImageTextTemplate = source.CustomLargeImageTextTemplate,
                UseCustomDetailsText = source.UseCustomDetailsText,
                CustomDetailsText = source.CustomDetailsText,
                UseCustomStateText = source.UseCustomStateText,
                CustomStateText = source.CustomStateText,
                HideSmallImage = source.HideSmallImage,
                UseCustomSmallImage = source.UseCustomSmallImage,
                CustomSmallImageUrl = source.CustomSmallImageUrl,
                UseCustomSmallImageText = source.UseCustomSmallImageText,
                CustomSmallImageTextTemplate = source.CustomSmallImageTextTemplate,
                LocationPrivacyMode = source.LocationPrivacyMode,
                MenuOverride = CloneContextOverride(source.MenuOverride),
                QueueOverride = CloneContextOverride(source.QueueOverride),
                OpenWorldOverride = CloneContextOverride(source.OpenWorldOverride),
                HousingOverride = CloneContextOverride(source.HousingOverride),
                DutyOverride = CloneContextOverride(source.DutyOverride),
                AfkOverride = CloneContextOverride(source.AfkOverride),
                JobOverrides = CloneJobOverrides(source.JobOverrides),
                ZoneOverrides = CloneZoneOverrides(source.ZoneOverrides),
                ShowStartTime = source.ShowStartTime,
                ResetTimeWhenChangingZones = source.ResetTimeWhenChangingZones,
                ShowJob = source.ShowJob,
                AbbreviateJob = source.AbbreviateJob,
                ShowLevel = source.ShowLevel,
                ShowParty = source.ShowParty,
                ShowAfk = source.ShowAfk,
                HideEntirelyWhenAfk = source.HideEntirelyWhenAfk,
                HideInCutscene = source.HideInCutscene,
                RPCBridgeEnabled = source.RPCBridgeEnabled,
            };
        }

        private static RichPresenceConfig NormalizeConfig(RichPresenceConfig config)
        {
            config.MenuOverride ??= new PresenceContextOverride();
            config.QueueOverride ??= new PresenceContextOverride();
            config.OpenWorldOverride ??= new PresenceContextOverride();
            config.HousingOverride ??= new PresenceContextOverride();
            config.DutyOverride ??= new PresenceContextOverride();
            config.AfkOverride ??= new PresenceContextOverride();
            config.JobOverrides ??= [];
            config.ZoneOverrides ??= [];
            config.Version = CurrentConfigVersion;
            return config;
        }

        private static PresenceContextOverride CloneContextOverride(PresenceContextOverride source)
        {
            return new PresenceContextOverride
            {
                Enabled = source.Enabled,
                LocationPrivacyMode = source.LocationPrivacyMode,
                UseDetailsTemplate = source.UseDetailsTemplate,
                DetailsTemplate = source.DetailsTemplate,
                UseStateTemplate = source.UseStateTemplate,
                StateTemplate = source.StateTemplate,
                LargeImageMode = source.LargeImageMode,
                LargeImageUrl = source.LargeImageUrl,
                UseLargeImageTextTemplate = source.UseLargeImageTextTemplate,
                LargeImageTextTemplate = source.LargeImageTextTemplate,
                SmallImageMode = source.SmallImageMode,
                SmallImageUrl = source.SmallImageUrl,
                UseSmallImageTextTemplate = source.UseSmallImageTextTemplate,
                SmallImageTextTemplate = source.SmallImageTextTemplate,
                HideParty = source.HideParty,
            };
        }

        private static List<PresenceJobOverride> CloneJobOverrides(List<PresenceJobOverride> source)
        {
            var clonedOverrides = new List<PresenceJobOverride>(source.Count);
            foreach (var jobOverride in source)
            {
                clonedOverrides.Add(new PresenceJobOverride
                {
                    Label = jobOverride.Label,
                    JobIds = [.. jobOverride.JobIds],
                    Enabled = jobOverride.Enabled,
                    LocationPrivacyMode = jobOverride.LocationPrivacyMode,
                    UseDetailsTemplate = jobOverride.UseDetailsTemplate,
                    DetailsTemplate = jobOverride.DetailsTemplate,
                    UseStateTemplate = jobOverride.UseStateTemplate,
                    StateTemplate = jobOverride.StateTemplate,
                    LargeImageMode = jobOverride.LargeImageMode,
                    LargeImageUrl = jobOverride.LargeImageUrl,
                    UseLargeImageTextTemplate = jobOverride.UseLargeImageTextTemplate,
                    LargeImageTextTemplate = jobOverride.LargeImageTextTemplate,
                    SmallImageMode = jobOverride.SmallImageMode,
                    SmallImageUrl = jobOverride.SmallImageUrl,
                    UseSmallImageTextTemplate = jobOverride.UseSmallImageTextTemplate,
                    SmallImageTextTemplate = jobOverride.SmallImageTextTemplate,
                    HideParty = jobOverride.HideParty,
                });
            }

            return clonedOverrides;
        }

        private static List<PresenceZoneOverride> CloneZoneOverrides(List<PresenceZoneOverride> source)
        {
            var clonedOverrides = new List<PresenceZoneOverride>(source.Count);
            foreach (var zoneOverride in source)
            {
                clonedOverrides.Add(new PresenceZoneOverride
                {
                    Label = zoneOverride.Label,
                    TerritoryIds = [.. zoneOverride.TerritoryIds],
                    Enabled = zoneOverride.Enabled,
                    LocationPrivacyMode = zoneOverride.LocationPrivacyMode,
                    UseDetailsTemplate = zoneOverride.UseDetailsTemplate,
                    DetailsTemplate = zoneOverride.DetailsTemplate,
                    UseStateTemplate = zoneOverride.UseStateTemplate,
                    StateTemplate = zoneOverride.StateTemplate,
                    LargeImageMode = zoneOverride.LargeImageMode,
                    LargeImageUrl = zoneOverride.LargeImageUrl,
                    UseLargeImageTextTemplate = zoneOverride.UseLargeImageTextTemplate,
                    LargeImageTextTemplate = zoneOverride.LargeImageTextTemplate,
                    SmallImageMode = zoneOverride.SmallImageMode,
                    SmallImageUrl = zoneOverride.SmallImageUrl,
                    UseSmallImageTextTemplate = zoneOverride.UseSmallImageTextTemplate,
                    SmallImageTextTemplate = zoneOverride.SmallImageTextTemplate,
                    HideParty = zoneOverride.HideParty,
                });
            }

            return clonedOverrides;
        }
    }
}
