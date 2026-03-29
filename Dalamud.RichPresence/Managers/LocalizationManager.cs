using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Dalamud.Game;
using Newtonsoft.Json;

using Dalamud.RichPresence.Models;

namespace Dalamud.RichPresence.Managers
{
    internal class LocalizationManager : IDisposable
    {
        private const string PREFIX = "dalamud_richpresence_";
        private const string DEFAULT_DICT_LANGCODE = "en";

        private CultureInfo clientCultureInfo = CultureInfo.InvariantCulture;
        private Dictionary<string, LocalizationEntry> clientLocalizationDictonary = new();
        private Dictionary<string, LocalizationEntry> pluginLocalizationDictionary = new();
        private readonly Dictionary<string, LocalizationEntry> defaultLocalizationDictionary;

        public LocalizationManager()
        {
            this.ReadClientLanguageLocFile(ClientLanguageToLangCode(RichPresencePlugin.ClientState.ClientLanguage));
            this.ReadPluginLanguageLocFile(RichPresencePlugin.DalamudPluginInterface.UiLanguage);
            this.defaultLocalizationDictionary = this.ReadFileWithLangCode(DEFAULT_DICT_LANGCODE);
            RichPresencePlugin.DalamudPluginInterface.LanguageChanged += this.ReadPluginLanguageLocFile;
        }

        public string Localize(string localizationStringKey, LocalizationLanguage localizationSource)
        {
            LocalizationEntry? dictValue;
            var entryFound = localizationSource == LocalizationLanguage.Client
                ? this.clientLocalizationDictonary.TryGetValue(localizationStringKey, out dictValue)
                : this.pluginLocalizationDictionary.TryGetValue(localizationStringKey, out dictValue);

            if (entryFound && !string.IsNullOrEmpty(dictValue?.Message))
            {
                return dictValue.Message;
            }

            return this.defaultLocalizationDictionary.TryGetValue(localizationStringKey, out dictValue)
                && !string.IsNullOrEmpty(dictValue?.Message)
                ? dictValue.Message
                : localizationStringKey;
        }

        public string TitleCase(string input) => this.clientCultureInfo.TextInfo.ToTitleCase(input);

        public void Dispose()
        {
            RichPresencePlugin.DalamudPluginInterface.LanguageChanged -= this.ReadPluginLanguageLocFile;
        }

        private void ReadClientLanguageLocFile(string langCode)
        {
            RichPresencePlugin.PluginLog.Debug("Loading client localization file...");
            this.clientLocalizationDictonary = this.ReadFileWithLangCode(langCode);
            this.clientCultureInfo = new CultureInfo(langCode);
            RichPresencePlugin.PluginLog.Debug("Client localization file loaded.");
        }

        private void ReadPluginLanguageLocFile(string langCode)
        {
            RichPresencePlugin.PluginLog.Debug("Loading plugin localization file...");
            this.pluginLocalizationDictionary = this.ReadFileWithLangCode(langCode);
            RichPresencePlugin.PluginLog.Debug("Plugin localization file loaded.");
        }

        private Dictionary<string, LocalizationEntry> ReadFileWithLangCode(string langCode)
        {
            try
            {
                RichPresencePlugin.PluginLog.Debug($"Reading localization file with language code {langCode}...");
                return this.ReadLocalizationFile(langCode);
            }
            catch (Exception ex)
            {
                RichPresencePlugin.PluginLog.Error(ex, $"File with language code {langCode} not loaded, using fallbacks...");
                return this.ReadLocalizationFile(DEFAULT_DICT_LANGCODE);
            }
        }

        private Dictionary<string, LocalizationEntry> ReadLocalizationFile(string langCode)
        {
            var assemblyDirectory = RichPresencePlugin.DalamudPluginInterface.AssemblyLocation.DirectoryName
                ?? throw new InvalidOperationException("Unable to resolve plugin assembly directory.");
            var locFilePath = Path.Combine(assemblyDirectory, "Resources", "loc", $"{PREFIX}{langCode}.json");

            return JsonConvert.DeserializeObject<Dictionary<string, LocalizationEntry>>(File.ReadAllText(locFilePath))
                ?? new Dictionary<string, LocalizationEntry>();
        }

        private string ClientLanguageToLangCode(ClientLanguage clientLanguage)
        {
            return clientLanguage switch
            {
                ClientLanguage.Japanese => "ja",
                ClientLanguage.German => "de",
                ClientLanguage.French => "fr",
                _ => "en",
            };
        }
    }
}
