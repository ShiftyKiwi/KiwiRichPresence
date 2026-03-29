using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Lumina.Extensions;

using KiwiRichPresence.Configuration;
using KiwiRichPresence.Models;

namespace KiwiRichPresence.Interface
{
    internal class RichPresenceConfigWindow
    {
        private sealed class JobOption
        {
            public uint RowId { get; init; }
            public string Label { get; init; } = string.Empty;
            public string ShortLabel { get; init; } = string.Empty;
        }

        private const int MaxCustomImageUrlLength = 256;
        private const int MaxRenderedPresenceTextLength = 128;
        private const int MaxTemplateInputLength = 512;

        private static readonly Vector4 WarningColor = new(1.0f, 0.75f, 0.3f, 1.0f);

        private static readonly PresenceLocationPrivacyMode[] GlobalPrivacyModes =
        [
            PresenceLocationPrivacyMode.Exact,
            PresenceLocationPrivacyMode.Region,
            PresenceLocationPrivacyMode.Generic,
        ];

        private static readonly string[] GlobalPrivacyModeLabels =
        [
            "Exact zone",
            "Region only",
            "Generic area",
        ];

        private static readonly PresenceLocationPrivacyMode[] ContextPrivacyModes =
        [
            PresenceLocationPrivacyMode.Inherit,
            PresenceLocationPrivacyMode.Exact,
            PresenceLocationPrivacyMode.Region,
            PresenceLocationPrivacyMode.Generic,
        ];

        private static readonly string[] ContextPrivacyModeLabels =
        [
            "Inherit global",
            "Exact zone",
            "Region only",
            "Generic area",
        ];

        private static readonly PresenceImageOverrideMode[] GlobalImageModes =
        [
            PresenceImageOverrideMode.Auto,
            PresenceImageOverrideMode.None,
            PresenceImageOverrideMode.CustomUrl,
        ];

        private static readonly string[] GlobalImageModeLabels =
        [
            "Auto",
            "Hide",
            "Custom URL",
        ];

        private static readonly PresenceImageOverrideMode[] ContextImageModes =
        [
            PresenceImageOverrideMode.Inherit,
            PresenceImageOverrideMode.Auto,
            PresenceImageOverrideMode.None,
            PresenceImageOverrideMode.CustomUrl,
        ];

        private static readonly string[] ContextImageModeLabels =
        [
            "Inherit global",
            "Auto",
            "Hide",
            "Custom URL",
        ];

        private bool IsOpen;
        private RichPresenceConfig RichPresenceConfig;
        private readonly List<JobOption> availableJobs;

        public RichPresenceConfigWindow()
        {
            this.availableJobs = RichPresencePlugin.DataManager.GetExcelSheet<ClassJob>()
                .Where(job => job.RowId > 0
                    && !string.IsNullOrWhiteSpace(job.Name.ExtractText())
                    && !string.IsNullOrWhiteSpace(job.Abbreviation.ExtractText()))
                .Select(job => new JobOption
                {
                    RowId = job.RowId,
                    Label = $"{job.Abbreviation.ExtractText()} - {RichPresencePlugin.LocalizationManager.TitleCase(job.Name.ExtractText())}",
                    ShortLabel = job.Abbreviation.ExtractText(),
                })
                .OrderBy(job => job.RowId)
                .ToList();
            this.RichPresenceConfig = CloneConfig(RichPresencePlugin.RichPresenceConfig);
        }

        public void DrawRichPresenceConfigWindow()
        {
            if (!this.IsOpen)
            {
                return;
            }

            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(940, 820), ImGuiCond.FirstUseEver);
            var isWindowOpen = ImGui.Begin(
                RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceConfiguration", LocalizationLanguage.Plugin),
                ref this.IsOpen,
                ImGuiWindowFlags.NoCollapse);

            if (!isWindowOpen)
            {
                ImGui.End();
                return;
            }

            var preview = RichPresencePlugin.GetPresencePreviewSnapshot(this.RichPresenceConfig);

            ImGui.TextWrapped(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresencePreface1", LocalizationLanguage.Plugin));
            ImGui.TextWrapped(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresencePreface2", LocalizationLanguage.Plugin));
            ImGui.Separator();

            ImGui.BeginChild("scrolling", ImGuiHelpers.ScaledVector2(0, 680), true, ImGuiWindowFlags.HorizontalScrollbar);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGuiHelpers.ScaledVector2(4, 6));

            this.DrawCoreOptions();
            ImGui.Separator();

            this.DrawGlobalOverrides();
            ImGui.Separator();

            this.DrawContextPresets();
            ImGui.Separator();

            this.DrawJobPresets();
            ImGui.Separator();

            this.DrawLivePreview(preview);

            if (Util.IsWine())
            {
                ImGui.Separator();
                ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceRPCBridgeEnabled", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.RPCBridgeEnabled);
                ImGui.TextColored(
                    ImGuiColors.DalamudGrey,
                    RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceRPCBridgeEnabledDetail", LocalizationLanguage.Plugin));
            }

            ImGui.PopStyleVar();
            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.Button("Apply/Test Now"))
            {
                RichPresencePlugin.ApplyCurrentConfiguration(CloneConfig(this.RichPresenceConfig), false);
                RichPresencePlugin.PluginLog.Information("Applied current Rich Presence settings without saving.");
            }

            ImGui.SameLine();
            if (ImGui.Button(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceSaveAndClose", LocalizationLanguage.Plugin)))
            {
                RichPresencePlugin.ApplyCurrentConfiguration(CloneConfig(this.RichPresenceConfig), true);
                RichPresencePlugin.PluginLog.Information("Settings saved.");
                this.Close();
            }

            ImGui.End();
        }

        public void Open()
        {
            this.RichPresenceConfig = CloneConfig(RichPresencePlugin.RichPresenceConfig);
            this.IsOpen = true;
        }

        public void Close()
        {
            this.IsOpen = false;
        }

        public void Toggle()
        {
            if (!this.IsOpen)
            {
                this.Open();
                return;
            }

            this.Close();
        }

        private void DrawCoreOptions()
        {
            if (!ImGui.CollapsingHeader("Core Options", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowName", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowName);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowFreeCompany", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowFreeCompany);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowWorld", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowWorld);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceAlwaysShowHomeWorld", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.AlwaysShowHomeWorld);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowDataCenter", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowDataCenter);

            ImGui.Separator();
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowStartTime", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowStartTime);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceResetTimeWhenChangingZones", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ResetTimeWhenChangingZones);

            ImGui.Separator();
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowLoginQueuePosition", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowLoginQueuePosition);
            ImGui.TextColored(
                ImGuiColors.DalamudGrey,
                RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowLoginQueuePositionDetail", LocalizationLanguage.Plugin));

            ImGui.Separator();
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowJob", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowJob);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceAbbreviateJob", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.AbbreviateJob);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowLevel", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowLevel);

            ImGui.Separator();
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowParty", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowParty);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceShowAFK", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.ShowAfk);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceHideAFKEntirely", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.HideEntirelyWhenAfk);
            ImGui.Checkbox(RichPresencePlugin.LocalizationManager.Localize("DalamudRichPresenceHideInCutscene", LocalizationLanguage.Plugin), ref this.RichPresenceConfig.HideInCutscene);
        }

        private void DrawGlobalOverrides()
        {
            if (!ImGui.CollapsingHeader("Global Overrides", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.TextWrapped("These settings apply everywhere unless a context preset overrides them.");
            ImGui.TextColored(ImGuiColors.DalamudGrey, RichPresencePlugin.GetTemplateTokenHelpText());

            DrawGlobalPrivacyCombo("Location privacy", ref this.RichPresenceConfig.LocationPrivacyMode);

            ImGui.Separator();
            DrawTemplateInput(
                "Global top line override",
                ref this.RichPresenceConfig.UseCustomDetailsText,
                ref this.RichPresenceConfig.CustomDetailsText,
                "Overrides Discord's top line (Details). Supports template tokens.");

            DrawTemplateInput(
                "Global second line override",
                ref this.RichPresenceConfig.UseCustomStateText,
                ref this.RichPresenceConfig.CustomStateText,
                "Overrides Discord's second line (State). Supports template tokens.");

            ImGui.Separator();
            DrawGlobalImageOverrideEditor(
                "Large image",
                ref this.RichPresenceConfig.HideLargeImage,
                ref this.RichPresenceConfig.UseCustomLargeImage,
                ref this.RichPresenceConfig.CustomLargeImageUrl,
                ref this.RichPresenceConfig.UseCustomLargeImageText,
                ref this.RichPresenceConfig.CustomLargeImageTextTemplate,
                "Controls the main artwork slot.");

            ImGui.Separator();
            DrawGlobalImageOverrideEditor(
                "Small image",
                ref this.RichPresenceConfig.HideSmallImage,
                ref this.RichPresenceConfig.UseCustomSmallImage,
                ref this.RichPresenceConfig.CustomSmallImageUrl,
                ref this.RichPresenceConfig.UseCustomSmallImageText,
                ref this.RichPresenceConfig.CustomSmallImageTextTemplate,
                "Controls the job/status icon slot.");
        }

        private void DrawContextPresets()
        {
            if (!ImGui.CollapsingHeader("Context Presets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.TextWrapped("Each preset can override text, images, privacy, and party display for a specific situation.");
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Presets currently exist for Menus, Login Queue, Open World, Housing, Duty, and AFK.");

            this.DrawContextOverrideEditor("Menus", this.RichPresenceConfig.MenuOverride, "Applies while logged out or sitting in menus.");
            this.DrawContextOverrideEditor("Login Queue", this.RichPresenceConfig.QueueOverride, "Applies while waiting in the login queue.");
            this.DrawContextOverrideEditor("Open World", this.RichPresenceConfig.OpenWorldOverride, "Applies during regular field play.");
            this.DrawContextOverrideEditor("Housing", this.RichPresenceConfig.HousingOverride, "Applies inside housing interiors.");
            this.DrawContextOverrideEditor("Duty", this.RichPresenceConfig.DutyOverride, "Applies in instanced duties.");
            this.DrawContextOverrideEditor("AFK", this.RichPresenceConfig.AfkOverride, "Applies when your current status is Away from Keyboard.");
        }

        private void DrawContextOverrideEditor(string label, PresenceContextOverride contextOverride, string description)
        {
            if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.PushID(label);

            ImGui.TextWrapped(description);
            this.DrawOverrideEditorSettings(
                contextOverride,
                "This preset is active for its context.",
                "This preset is currently disabled.");

            ImGui.PopID();
        }

        private void DrawJobPresets()
        {
            if (!ImGui.CollapsingHeader("Job Presets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.TextWrapped("Job presets let you swap text, images, privacy, and party visibility based on the class or job you are currently on.");
            ImGui.TextColored(ImGuiColors.DalamudGrey, "The first enabled matching job preset from top to bottom wins, then context presets can still override it.");

            if (ImGui.Button("Add job preset"))
            {
                this.RichPresenceConfig.JobOverrides.Add(new PresenceJobOverride
                {
                    Label = $"Job Preset {this.RichPresenceConfig.JobOverrides.Count + 1}",
                });
            }

            if (this.RichPresenceConfig.JobOverrides.Count == 0)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No job presets yet.");
                return;
            }

            int moveUpIndex = -1;
            int moveDownIndex = -1;
            int removeIndex = -1;

            for (var i = 0; i < this.RichPresenceConfig.JobOverrides.Count; i++)
            {
                var jobOverride = this.RichPresenceConfig.JobOverrides[i];
                var headerLabel = $"{GetJobPresetDisplayName(jobOverride, i)} [{GetSelectedJobsSummary(jobOverride)}]###jobPreset_{i}";
                if (!ImGui.CollapsingHeader(headerLabel, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    continue;
                }

                ImGui.PushID($"jobPresetBody_{i}");

                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("Preset name", ref jobOverride.Label, MaxTemplateInputLength);

                if (ImGui.SmallButton("Move up") && i > 0)
                {
                    moveUpIndex = i;
                }

                ImGui.SameLine();
                if (ImGui.SmallButton("Move down") && i < this.RichPresenceConfig.JobOverrides.Count - 1)
                {
                    moveDownIndex = i;
                }

                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    removeIndex = i;
                }

                this.DrawJobSelectionEditor(jobOverride);
                this.DrawOverrideEditorSettings(
                    jobOverride,
                    "This preset is active when one of its selected jobs matches.",
                    "This job preset is currently disabled.");

                ImGui.PopID();
            }

            if (moveUpIndex > 0)
            {
                (this.RichPresenceConfig.JobOverrides[moveUpIndex - 1], this.RichPresenceConfig.JobOverrides[moveUpIndex]) =
                    (this.RichPresenceConfig.JobOverrides[moveUpIndex], this.RichPresenceConfig.JobOverrides[moveUpIndex - 1]);
            }

            if (moveDownIndex >= 0 && moveDownIndex < this.RichPresenceConfig.JobOverrides.Count - 1)
            {
                (this.RichPresenceConfig.JobOverrides[moveDownIndex], this.RichPresenceConfig.JobOverrides[moveDownIndex + 1]) =
                    (this.RichPresenceConfig.JobOverrides[moveDownIndex + 1], this.RichPresenceConfig.JobOverrides[moveDownIndex]);
            }

            if (removeIndex >= 0)
            {
                this.RichPresenceConfig.JobOverrides.RemoveAt(removeIndex);
            }
        }

        private void DrawJobSelectionEditor(PresenceJobOverride jobOverride)
        {
            ImGui.Separator();
            ImGui.TextWrapped("Target jobs");
            var selectedJobsSummary = GetSelectedJobsSummary(jobOverride);
            ImGui.TextColored(
                jobOverride.JobIds.Count > 0 ? ImGuiColors.HealerGreen : WarningColor,
                jobOverride.JobIds.Count > 0
                    ? $"Selected: {selectedJobsSummary}"
                    : "Select at least one job for this preset to match.");

            if (this.availableJobs.Count == 0)
            {
                ImGui.TextColored(WarningColor, "No class/job data was available to populate the selector.");
                return;
            }

            if (ImGui.BeginTable("jobPresetTargets", 4, ImGuiTableFlags.SizingStretchProp))
            {
                foreach (var jobOption in this.availableJobs)
                {
                    ImGui.TableNextColumn();
                    var isSelected = jobOverride.JobIds.Contains(jobOption.RowId);
                    if (ImGui.Checkbox(jobOption.Label, ref isSelected))
                    {
                        ToggleJobSelection(jobOverride, jobOption.RowId, isSelected);
                    }
                }

                ImGui.EndTable();
            }
        }

        private void DrawLivePreview(PresencePreviewSnapshot preview)
        {
            if (!ImGui.CollapsingHeader("Live Preview", ImGuiTreeNodeFlags.DefaultOpen))
            {
                return;
            }

            ImGui.TextWrapped("This preview is generated from your current in-game state plus the unsaved settings shown above.");

            if (!preview.IsAvailable)
            {
                ImGui.TextColored(WarningColor, "Preview is unavailable right now.");
                return;
            }

            if (preview.ClearsPresence)
            {
                ImGui.TextColored(WarningColor, "The current rules would clear Rich Presence entirely in this context.");
            }

            DrawPreviewLine("Context", preview.ContextLabel);
            DrawPreviewLine("Active preset", preview.ActivePresetLabel);
            DrawPreviewLine("Details", preview.Details);
            DrawPreviewLine("State", preview.State);
            DrawPreviewLine("Large image key/url", NormalizePreviewValue(preview.LargeImageKey));
            DrawPreviewLine("Large image tooltip", NormalizePreviewValue(preview.LargeImageText));
            DrawPreviewLine("Small image key/url", NormalizePreviewValue(preview.SmallImageKey));
            DrawPreviewLine("Small image tooltip", NormalizePreviewValue(preview.SmallImageText));
            DrawPreviewLine("Party", NormalizePreviewValue(preview.PartyText));
        }

        private static void DrawPreviewLine(string label, string value)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, $"{label}:");
            ImGui.SameLine();
            ImGui.TextWrapped(value);
        }

        private void DrawOverrideEditorSettings(PresenceContextOverride contextOverride, string enabledText, string disabledText)
        {
            ImGui.Checkbox("Enable this preset", ref contextOverride.Enabled);
            ImGui.TextColored(
                contextOverride.Enabled ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey,
                contextOverride.Enabled ? enabledText : disabledText);

            DrawContextPrivacyCombo("Location privacy", ref contextOverride.LocationPrivacyMode);
            ImGui.Checkbox("Hide party data in this preset", ref contextOverride.HideParty);

            ImGui.Separator();
            DrawTemplateInput(
                "Top line template",
                ref contextOverride.UseDetailsTemplate,
                ref contextOverride.DetailsTemplate,
                "Overrides the top line when this preset is active.");

            DrawTemplateInput(
                "Second line template",
                ref contextOverride.UseStateTemplate,
                ref contextOverride.StateTemplate,
                "Overrides the second line when this preset is active.");

            ImGui.Separator();
            DrawContextImageOverrideEditor(
                "Large image",
                ref contextOverride.LargeImageMode,
                ref contextOverride.LargeImageUrl,
                ref contextOverride.UseLargeImageTextTemplate,
                ref contextOverride.LargeImageTextTemplate);

            ImGui.Separator();
            DrawContextImageOverrideEditor(
                "Small image",
                ref contextOverride.SmallImageMode,
                ref contextOverride.SmallImageUrl,
                ref contextOverride.UseSmallImageTextTemplate,
                ref contextOverride.SmallImageTextTemplate);
        }

        private static string NormalizePreviewValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Hidden / empty" : value;
        }

        private static void DrawTemplateInput(string label, ref bool isEnabled, ref string template, string detail)
        {
            ImGui.Checkbox($"Enable {label}", ref isEnabled);
            ImGui.TextColored(ImGuiColors.DalamudGrey, detail);
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText(label, ref template, MaxTemplateInputLength);

            if (isEnabled && !string.IsNullOrWhiteSpace(template))
            {
                var byteCount = Encoding.UTF8.GetByteCount(template.Trim());
                var message = byteCount <= MaxRenderedPresenceTextLength
                    ? $"Current entry is {byteCount} UTF-8 bytes before token expansion. Discord accepts up to {MaxRenderedPresenceTextLength} bytes after rendering."
                    : $"Current entry is {byteCount} UTF-8 bytes before token expansion. That's fine if tokens resolve shorter, but rendered output still must stay within {MaxRenderedPresenceTextLength} bytes.";
                var color = byteCount <= MaxRenderedPresenceTextLength ? ImGuiColors.HealerGreen : WarningColor;
                ImGui.TextColored(color, message);
            }
        }

        private static void DrawGlobalImageOverrideEditor(
            string label,
            ref bool hideImage,
            ref bool useCustomImage,
            ref string customImageUrl,
            ref bool useCustomImageText,
            ref string customImageTextTemplate,
            string detail)
        {
            DrawGlobalImageModeCombo($"{label} mode", ref hideImage, ref useCustomImage);
            ImGui.TextColored(ImGuiColors.DalamudGrey, detail);

            if (useCustomImage)
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText($"{label} custom URL", ref customImageUrl, MaxCustomImageUrlLength);
                DrawImageUrlHint(customImageUrl);
            }

            DrawTemplateInput(
                $"{label} hover text template",
                ref useCustomImageText,
                ref customImageTextTemplate,
                $"Tooltip shown when hovering the {label.ToLowerInvariant()}.");
        }

        private static void DrawContextImageOverrideEditor(
            string label,
            ref PresenceImageOverrideMode imageMode,
            ref string customImageUrl,
            ref bool useImageTextTemplate,
            ref string imageTextTemplate)
        {
            DrawContextImageModeCombo($"{label} mode", ref imageMode);

            if (imageMode == PresenceImageOverrideMode.CustomUrl)
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText($"{label} custom URL", ref customImageUrl, MaxCustomImageUrlLength);
                DrawImageUrlHint(customImageUrl);
            }

            DrawTemplateInput(
                $"{label} hover text template",
                ref useImageTextTemplate,
                ref imageTextTemplate,
                $"Tooltip shown when hovering the {label.ToLowerInvariant()}.");
        }

        private static void DrawImageUrlHint(string customImageUrl)
        {
            if (string.IsNullOrWhiteSpace(customImageUrl))
            {
                return;
            }

            var isValidCustomImageUrl = IsValidCustomImageUrl(customImageUrl);
            ImGui.TextColored(
                isValidCustomImageUrl ? ImGuiColors.HealerGreen : WarningColor,
                isValidCustomImageUrl
                    ? "This looks like a valid public http(s) image URL."
                    : "Enter a direct public http(s) image URL up to 256 characters long.");
        }

        private static void DrawGlobalPrivacyCombo(string label, ref PresenceLocationPrivacyMode privacyMode)
        {
            var currentIndex = Array.IndexOf(GlobalPrivacyModes, privacyMode);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (ImGui.Combo(label, ref currentIndex, GlobalPrivacyModeLabels, GlobalPrivacyModeLabels.Length))
            {
                privacyMode = GlobalPrivacyModes[currentIndex];
            }
        }

        private static void DrawContextPrivacyCombo(string label, ref PresenceLocationPrivacyMode privacyMode)
        {
            var currentIndex = Array.IndexOf(ContextPrivacyModes, privacyMode);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (ImGui.Combo(label, ref currentIndex, ContextPrivacyModeLabels, ContextPrivacyModeLabels.Length))
            {
                privacyMode = ContextPrivacyModes[currentIndex];
            }
        }

        private static void DrawGlobalImageModeCombo(string label, ref bool hideImage, ref bool useCustomImage)
        {
            var currentMode = hideImage
                ? PresenceImageOverrideMode.None
                : useCustomImage ? PresenceImageOverrideMode.CustomUrl : PresenceImageOverrideMode.Auto;
            var currentIndex = Array.IndexOf(GlobalImageModes, currentMode);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (ImGui.Combo(label, ref currentIndex, GlobalImageModeLabels, GlobalImageModeLabels.Length))
            {
                hideImage = GlobalImageModes[currentIndex] == PresenceImageOverrideMode.None;
                useCustomImage = GlobalImageModes[currentIndex] == PresenceImageOverrideMode.CustomUrl;
            }
        }

        private static void DrawContextImageModeCombo(string label, ref PresenceImageOverrideMode imageMode)
        {
            var currentIndex = Array.IndexOf(ContextImageModes, imageMode);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (ImGui.Combo(label, ref currentIndex, ContextImageModeLabels, ContextImageModeLabels.Length))
            {
                imageMode = ContextImageModes[currentIndex];
            }
        }

        private static bool IsValidCustomImageUrl(string customImageUrl)
        {
            var trimmedCustomImageUrl = customImageUrl.Trim();
            if (trimmedCustomImageUrl.Length is <= 0 or > MaxCustomImageUrlLength)
            {
                return false;
            }

            return Uri.TryCreate(trimmedCustomImageUrl, UriKind.Absolute, out var parsedUrl)
                && (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps);
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

        private static string GetJobPresetDisplayName(PresenceJobOverride jobOverride, int index)
        {
            return string.IsNullOrWhiteSpace(jobOverride.Label)
                ? $"Job Preset {index + 1}"
                : jobOverride.Label.Trim();
        }

        private string GetSelectedJobsSummary(PresenceJobOverride jobOverride)
        {
            if (jobOverride.JobIds.Count == 0)
            {
                return "No jobs selected";
            }

            var selectedJobs = this.availableJobs
                .Where(jobOption => jobOverride.JobIds.Contains(jobOption.RowId))
                .Select(jobOption => jobOption.ShortLabel)
                .ToList();

            return selectedJobs.Count == 0
                ? "No jobs selected"
                : string.Join(", ", selectedJobs);
        }

        private static void ToggleJobSelection(PresenceJobOverride jobOverride, uint jobId, bool isSelected)
        {
            if (isSelected)
            {
                if (!jobOverride.JobIds.Contains(jobId))
                {
                    jobOverride.JobIds.Add(jobId);
                    jobOverride.JobIds.Sort();
                }

                return;
            }

            jobOverride.JobIds.Remove(jobId);
        }
    }
}
