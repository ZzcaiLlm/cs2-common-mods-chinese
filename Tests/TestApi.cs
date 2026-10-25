using System;
using System.Collections.Generic;

namespace Game
{
    public class UpdateSystem { }
}

namespace Game.Modding
{
    public interface IMod
    {
        void OnLoad(Game.UpdateSystem updateSystem);
        void OnDispose();
    }

    public class ModSetting
    {
        public string GetSettingsLocaleID() { return "Options.SECTION[Placemaker.Mod]"; }
        public string GetOptionTabLocaleID(string name) { return "Options.TAB[Placemaker.Mod." + name + "]"; }
        public string GetOptionGroupLocaleID(string name) { return "Options.GROUP[Placemaker.Mod." + name + "]"; }
        public string GetOptionLabelLocaleID(string name) { return "Options.OPTION[Placemaker.Mod.PlacemakerSettings." + name + "]"; }
        public string GetOptionDescLocaleID(string name) { return "Options.OPTION_DESCRIPTION[Placemaker.Mod.PlacemakerSettings." + name + "]"; }
        public string GetOptionWarningLocaleID(string name) { return "Options.WARNING[Placemaker.Mod.PlacemakerSettings." + name + "]"; }
        public string GetEnumValueLocaleID<T>(T value)
        {
            return "Options.Placemaker.Mod." + typeof(T).Name.ToUpper() + "[" + value + "]";
        }
        public string GetBindingMapLocaleID() { return "Options.INPUT_MAP[Placemaker.Mod]"; }
        public string GetBindingKeyLocaleID(string name) { return "Options.OPTION[Placemaker.Mod/" + name + "/Button]"; }
    }
}

namespace Game.SceneFlow
{
    public class GameManager
    {
        public static GameManager instance = new GameManager();
        public Colossal.Localization.LocalizationManager localizationManager;
    }
}

namespace Colossal.Logging
{
    public interface ILog
    {
        ILog SetShowsErrorsInUI(bool value);
        void Info(object value);
        void Warn(Exception error, object value);
    }
    public static class LogManager
    {
        public static ILog GetLogger(string name) { return new TestLog(); }
    }
    internal class TestLog : ILog
    {
        public ILog SetShowsErrorsInUI(bool value) { return this; }
        public void Info(object value) { }
        public void Warn(Exception error, object value) { throw new Exception(value.ToString(), error); }
    }
}

namespace Colossal.Localization
{
    public class MemorySource
    {
        public readonly Dictionary<string, string> Entries;
        public MemorySource(Dictionary<string, string> entries) { Entries = entries; }
    }
    public class LocalizationDictionary
    {
        public readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool TryGetValue(string key, out string value) { return Entries.TryGetValue(key, out value); }
    }
    public class LocalizationManager
    {
        private readonly Dictionary<string, List<MemorySource>> sources = new Dictionary<string, List<MemorySource>>();
        private readonly List<KeyValuePair<string, MemorySource>> history = new List<KeyValuePair<string, MemorySource>>();
        public event Action onActiveDictionaryChanged;
        public event Action onSupportedLocalesChanged;
        public string activeLocaleId { get; private set; }
        public LocalizationDictionary activeDictionary { get; private set; }
        public int SourceAdds { get; private set; }
        public bool DeferredEvents;
        private readonly Queue<Action> events = new Queue<Action>();
        public LocalizationManager(string locale)
        {
            activeLocaleId = locale;
            activeDictionary = new LocalizationDictionary();
        }
        public void AddSource(string locale, MemorySource source)
        {
            List<MemorySource> list;
            if (!sources.TryGetValue(locale, out list))
                sources[locale] = list = new List<MemorySource>();
            if (list.Contains(source))
                return;
            list.Add(source);
            history.Add(new KeyValuePair<string, MemorySource>(locale, source));
            SourceAdds++;
            if (locale == activeLocaleId)
            {
                foreach (KeyValuePair<string, string> entry in source.Entries)
                    activeDictionary.Entries[entry.Key] = entry.Value;
                Notify(onActiveDictionaryChanged);
            }
        }
        public void RemoveSource(string locale, MemorySource source)
        {
            List<MemorySource> list;
            if (sources.TryGetValue(locale, out list))
                list.Remove(source);
            if (locale == activeLocaleId)
                SetActiveLocale(locale);
        }
        public void SetActiveLocale(string locale)
        {
            activeLocaleId = locale;
            activeDictionary = new LocalizationDictionary();
            List<MemorySource> list;
            if (sources.TryGetValue(locale, out list))
                foreach (MemorySource source in list)
                    foreach (KeyValuePair<string, string> entry in source.Entries)
                        activeDictionary.Entries[entry.Key] = entry.Value;
            Notify(onActiveDictionaryChanged);
        }
        public void SupportedLocalesChanged() { Notify(onSupportedLocalesChanged); }
        public void RebuildLocaleCatalog()
        {
            sources.Clear();
            foreach (KeyValuePair<string, MemorySource> pair in history)
            {
                List<MemorySource> list;
                if (!sources.TryGetValue(pair.Key, out list))
                    sources[pair.Key] = list = new List<MemorySource>();
                list.Add(pair.Value);
            }
            SetActiveLocale(activeLocaleId);
            SupportedLocalesChanged();
        }
        private void Notify(Action action)
        {
            if (action == null)
                return;
            if (DeferredEvents)
                events.Enqueue(action);
            else
                action();
        }
        public void Flush()
        {
            int count = 0;
            while (events.Count != 0)
            {
                if (++count > 100)
                    throw new Exception("Localization events did not settle.");
                events.Dequeue()();
            }
        }
        public int SourceCount(string locale)
        {
            List<MemorySource> list;
            return sources.TryGetValue(locale, out list) ? list.Count : 0;
        }
    }
}
