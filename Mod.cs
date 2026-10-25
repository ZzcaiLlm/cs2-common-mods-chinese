using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Colossal.Localization;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

[assembly: AssemblyTitle("Placemaker Chinese")]
[assembly: AssemblyDescription("Simplified Chinese localization for Placemaker, common mod settings, pedestrian overpasses and civicbeaver assets")]
[assembly: AssemblyCompany("Local translation")]
[assembly: AssemblyProduct("Placemaker Chinese")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",
    FrameworkDisplayName = ".NET Framework 4.8")]

namespace PlacemakerChinese
{
    public sealed class Mod : IMod
    {
        private const string Locale = "zh-HANS";
        private static readonly ILog Log =
            LogManager.GetLogger("PlacemakerChinese").SetShowsErrorsInUI(false);
        private static readonly HashSet<string> CivicbeaverAssetNames = LoadAssetNames();
        private static readonly HashSet<string> ExtraKeys = LoadExtraKeys();

        private List<MemorySource> sources;
        private LocalizationManager manager;
        private Dictionary<string, string> entries;
        private ModSetting settings;
        private bool updating;
        private bool disposed;

        public void OnLoad(UpdateSystem updateSystem)
        {
            // CS2 uses FormatterServices.GetUninitializedObject for IMod instances.
            // Instance field initializers and constructors are not executed.
            sources = new List<MemorySource>();
            disposed = false;
            updating = false;
            entries = LoadEntries();
            int translationCount = entries.Count;
            manager = GameManager.instance == null ? null : GameManager.instance.localizationManager;
            if (manager == null)
                throw new InvalidOperationException("The game localization manager is unavailable.");

            manager.onActiveDictionaryChanged += Refresh;
            manager.onSupportedLocalesChanged += Refresh;
            updating = true;
            try
            {
                AddSettingsAliases();
                RegisterSource();
            }
            finally
            {
                updating = false;
            }
            Refresh();
            Log.Info("Placemaker Chinese " + typeof(Mod).Assembly.GetName().Version.ToString(3) +
                " loaded: " + translationCount + " translations, locale " + Locale +
                ". I18n Everywhere is not required.");
        }

        public void OnDispose()
        {
            disposed = true;
            if (manager != null)
            {
                manager.onActiveDictionaryChanged -= Refresh;
                manager.onSupportedLocalesChanged -= Refresh;
                if (sources != null)
                {
                    foreach (MemorySource source in sources)
                    {
                        try { manager.RemoveSource(Locale, source); }
                        catch (Exception error)
                        {
                            Log.Warn(error, "Could not remove a translation source during cleanup.");
                        }
                    }
                }
            }
            if (sources != null)
                sources.Clear();
            // The game retains user source references when rebuilding its locale catalog.
            // Empty their shared dictionary so a disposed translation cannot reappear.
            if (entries != null)
                entries.Clear();
            manager = null;
            settings = null;
            entries = null;
            Log.Info("Placemaker Chinese unloaded.");
        }

        private void Refresh()
        {
            if (disposed || updating || manager == null)
                return;

            updating = true;
            try
            {
                AddSettingsAliases();
                if (!String.Equals(manager.activeLocaleId, Locale, StringComparison.OrdinalIgnoreCase))
                    return;

                LocalizationDictionary dictionary = manager.activeDictionary;
                foreach (KeyValuePair<string, string> entry in entries)
                {
                    string current;
                    if (!dictionary.TryGetValue(entry.Key, out current) ||
                        !String.Equals(current, entry.Value, StringComparison.Ordinal))
                    {
                        // Placemaker registers English fallbacks for every locale. A new source
                        // goes after those fallbacks even when Placemaker loads after this mod.
                        RegisterSource();
                        Log.Info("Applied Chinese translations after a localization refresh.");
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                Log.Warn(error, "Could not refresh Placemaker Chinese.");
            }
            finally
            {
                updating = false;
            }
        }

        private void RegisterSource()
        {
            MemorySource source = new MemorySource(entries);
            sources.Add(source);
            manager.AddSource(Locale, source);
        }

        private void AddSettingsAliases()
        {
            ModSetting current = FindPlacemakerSettings();
            if (current == null || Object.ReferenceEquals(current, settings))
                return;

            Put(current.GetSettingsLocaleID(), "Placemaker.Name");
            Put(current.GetOptionTabLocaleID("Main"), "Placemaker.Settings.Tab.Main");
            foreach (string group in new[] { "General", "Brush", "Build", "Neighbourhood", "Keys", "Maintenance" })
                Put(current.GetOptionGroupLocaleID(group), "Placemaker.Settings.Group." + group);

            PropertyInfo[] properties = current.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (PropertyInfo property in properties)
            {
                string key = "Placemaker.Settings." + property.Name;
                Put(current.GetOptionLabelLocaleID(property.Name), key);
                Put(current.GetOptionDescLocaleID(property.Name), key + ".Desc");
                Put(current.GetOptionWarningLocaleID(property.Name), key + ".Warning");
            }

            Type preview = current.GetType().Assembly.GetType("Placemaker.Game.Settings.PreviewDetail");
            if (preview != null && preview.IsEnum)
            {
                MethodInfo method = typeof(ModSetting).GetMethod("GetEnumValueLocaleID");
                if (method != null)
                {
                    method = method.MakeGenericMethod(preview);
                    foreach (object value in Enum.GetValues(preview))
                        Put((string)method.Invoke(current, new[] { value }),
                            "Placemaker.Settings.PreviewDetail." + value);
                }
            }

            Put(current.GetBindingMapLocaleID(), "Placemaker.Name");
            foreach (string action in new[] { "ToggleTool", "Primary", "Undo", "Redo", "RedoAlt" })
                Put(current.GetBindingKeyLocaleID(action), "Placemaker.Settings.Action." + action);

            settings = current;
            Log.Info("Placemaker settings and key binding translations registered.");
        }

        private static ModSetting FindPlacemakerSettings()
        {
            // No compile-time dependency on Placemaker: an absent or disabled original mod
            // must not prevent the game from loading this translation assembly.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!String.Equals(assembly.GetName().Name, "Placemaker", StringComparison.OrdinalIgnoreCase))
                    continue;
                Type registration = assembly.GetType("Placemaker.Game.Tool.ToolRegistration");
                if (registration == null)
                    return null;
                PropertyInfo property = registration.GetProperty("Settings",
                    BindingFlags.Public | BindingFlags.Static);
                return property == null ? null : property.GetValue(null, null) as ModSetting;
            }
            return null;
        }

        private void Put(string target, string key)
        {
            string value;
            if (entries.TryGetValue(key, out value))
                entries[target] = value;
        }

        private static Dictionary<string, string> LoadEntries()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream("PlacemakerChinese.zh-HANS.bin"))
            {
                if (stream == null)
                    throw new InvalidDataException("The embedded Chinese translations are missing.");
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (reader.ReadString() != "PlacemakerChinese/1")
                        throw new InvalidDataException("Unsupported translation format.");
                    int count = reader.ReadInt32();
                    if (count < 1 || count > 100000)
                        throw new InvalidDataException("Invalid translation entry count.");
                    for (int index = 0; index < count; index++)
                    {
                        string key = reader.ReadString();
                        string value = reader.ReadString();
                        if (!IsOwnedKey(key))
                            throw new InvalidDataException("Translation key outside supported mods and assets: " + key);
                        result.Add(key, value);
                    }
                }
            }
            return result;
        }

        private static bool IsOwnedKey(string key)
        {
            if (ExtraKeys.Contains(key))
                return true;
            if (key.StartsWith("Placemaker.", StringComparison.Ordinal) ||
                key == "Assets.NAME[Placemaker Area]" ||
                key == "Assets.DESCRIPTION[Placemaker Area]")
                return true;
            foreach (string id in new[] {
                "Pedestrian Ramp Pass S", "Pedestrian Ramp Pass M", "Pedestrian Ramp Pass L",
                "Pedestrian Ramp Left", "Pedestrian Ramp Right"
            })
                if (key == "Assets.NAME[" + id + "]" || key == "Assets.DESCRIPTION[" + id + "]")
                    return true;
            foreach (string prefix in new[] { "Assets.NAME[", "Assets.DESCRIPTION[" })
                if (key.StartsWith(prefix, StringComparison.Ordinal) &&
                    key.EndsWith("]", StringComparison.Ordinal) &&
                    CivicbeaverAssetNames.Contains(key.Substring(prefix.Length, key.Length - prefix.Length - 1)))
                    return true;
            return false;
        }

        private static HashSet<string> LoadExtraKeys()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream(
                "PlacemakerChinese.ExtraKeys.txt"))
            {
                if (stream == null)
                    throw new InvalidDataException("The custom translation key inventory is missing.");
                using (StreamReader reader = new StreamReader(stream))
                {
                    string key;
                    while ((key = reader.ReadLine()) != null)
                        if (String.IsNullOrWhiteSpace(key) || key.Length > 512 || !result.Add(key))
                            throw new InvalidDataException("Invalid or duplicate custom translation key.");
                }
            }
            return result;
        }

        private static HashSet<string> LoadAssetNames()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream(
                "PlacemakerChinese.CivicbeaverAssets.txt"))
            {
                if (stream == null)
                    throw new InvalidDataException("The supported civicbeaver asset inventory is missing.");
                using (StreamReader reader = new StreamReader(stream))
                {
                    string name;
                    while ((name = reader.ReadLine()) != null)
                        if (name.Length == 0 || !result.Add(name))
                            throw new InvalidDataException("Empty or duplicate supported asset name.");
                }
            }
            if (result.Count != 106)
                throw new InvalidDataException("The supported civicbeaver asset inventory is incomplete.");
            return result;
        }
    }
}
