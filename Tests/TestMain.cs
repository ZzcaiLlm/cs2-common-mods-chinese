using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Colossal.Localization;
using Game.Modding;
using Game.SceneFlow;

internal static class TestMain
{
    private static int passed;
    private static Dictionary<string, string> translations;
    private static Type registration;
    private static Type settingType;

    public static int Main()
    {
        try
        {
            MethodInfo loader = typeof(PlacemakerChinese.Mod).GetMethod("LoadEntries",
                BindingFlags.NonPublic | BindingFlags.Static);
            translations = (Dictionary<string, string>)loader.Invoke(null, null);
            Check(translations.Count >= 8181, "Embedded data retains the original translation inventory");
            Run("Dynamic UI translations and placeholders are covered", DynamicUiCoverage);
            Run("Six installed mods' missing settings and descriptions are covered", SettingsCoverage);
            Run("Uninitialized and failed-load instances dispose safely", DisposeBeforeLoad);
            Run("Translation loads with no original mod present", NoOriginal);
            Assembly original = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Placemaker.dll"));
            registration = original.GetType("Placemaker.Game.Tool.ToolRegistration");
            settingType = original.GetType("Placemaker.Game.Settings.PlacemakerSettings");
            Run("Original mod loads first; settings, warnings, enum and bindings translate", OriginalFirst);
            Run("Translation loads first; English fallback is overridden", TranslationFirst);
            Run("Language changes preserve English and restore Chinese", LanguageSwitch);
            Run("Deferred localization events settle without repeated sources", DeferredEvents);
            Run("Unload removes every translation source and event handler", Unload);
            Console.WriteLine("PASS: " + passed + " checks.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Check(bool value, string name)
    {
        if (!value)
            throw new Exception("FAIL: " + name);
        passed++;
    }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine("PASS: " + name);
    }

    private static PlacemakerChinese.Mod CreateMod()
    {
        return (PlacemakerChinese.Mod)FormatterServices.GetUninitializedObject(typeof(PlacemakerChinese.Mod));
    }

    private static void DynamicUiCoverage()
    {
        // This key inventory was extracted independently from Placemaker 0.2.0's
        // TSX, built-in styles, initial UI state and asset catalog.
        using (Stream stream = typeof(TestMain).Assembly.GetManifestResourceStream(
            "PlacemakerChinese.Tests.RequiredUiKeys.txt"))
        using (StreamReader reader = new StreamReader(stream))
        {
            int count = 0;
            string key;
            while ((key = reader.ReadLine()) != null)
            {
                if (key.Length == 0) continue;
                string value;
                Check(translations.TryGetValue(key, out value) && !String.IsNullOrWhiteSpace(value),
                    "Required dynamic UI key: " + key);
                count++;
            }
            Check(count == 260, "The reference inventory includes all 260 supplementary keys");
        }
        foreach (string key in new[] { "Placemaker.UI.Polish.TabNumbers",
            "Placemaker.UI.Polish.Knob.sideReach", "Placemaker.UI.Painter.Theme.officeTowers",
            "Placemaker.UI.Marker.Entry", "Assets.NAME[Pedestrian Ramp Pass S]",
            "Placemaker.UI.Polish.Kind.homes", "Placemaker.UI.Polish.Kind.shops",
            "Placemaker.UI.Polish.Kind.offices", "Placemaker.UI.Polish.Kind.industry",
            "Assets.NAME[BWArcade01]", "Assets.NAME[BWArcade02]", "Assets.NAME[TargetCart]",
            "Assets.NAME[BeaverdaleMallWing02Roof]" })
            Check(!String.IsNullOrWhiteSpace(translations[key]), "Translation retained: " + key);
        Check(String.Format(translations["Placemaker.UI.Outgrow.Cost2"], "1.5").Contains("1.5"),
            "Frame-time placeholder is retained");
        string formatted = String.Format(translations["Placemaker.UI.Outgrow.OfAll"], 4321, 9876);
        Check(formatted.Contains("4321") && formatted.Contains("9876"), "Both statistics placeholders are retained");
        Check(String.Format(translations["Placemaker.UI.Polish.OutgrowSome.Title2"], "住宅").Contains("住宅"),
            "Confirmation placeholder is retained");
        Check(translations.ContainsKey("Assets.DESCRIPTION[Pedestrian Ramp Right]"),
            "Overpass part description is available");
        MethodInfo scope = typeof(PlacemakerChinese.Mod).GetMethod("IsOwnedKey",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (!translations.ContainsKey("Assets.NAME[UnrelatedAsset]"))
            Check(!(bool)scope.Invoke(null, new object[] { "Assets.NAME[UnrelatedAsset]" }),
                "Unrelated assets remain outside the translation scope");
        foreach (string key in new[] { "Assets.NAME[Costco Wholesale Extra]",
            "Assets.NAME[costco wholesale]", "Assets.NAME[Boardwalk 6x5 Mesh]",
            "Assets.DESCRIPTION[Apple Store]Trailing", "Assets.NAME[]", "OtherMod.IKEA" })
            if (!translations.ContainsKey(key))
                Check(!(bool)scope.Invoke(null, new object[] { key }),
                    "Asset ownership uses exact, case-sensitive names: " + key);
        foreach (string key in translations.Keys)
            Check((bool)scope.Invoke(null, new object[] { key }), "Every embedded key is explicitly accepted: " + key);
        Check((bool)scope.Invoke(null, new object[] { "Assets.NAME[Costco Wholesale]" }) &&
            (bool)scope.Invoke(null, new object[] { "Assets.DESCRIPTION[Apple Store]" }),
            "Supported asset name and description keys are accepted");
    }

    private static void DisposeBeforeLoad()
    {
        CreateMod().OnDispose();
        GameManager.instance.localizationManager = null;
        PlacemakerChinese.Mod mod = CreateMod();
        bool failed = false;
        try { mod.OnLoad(null); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "A missing localization manager reports the load failure");
        mod.OnDispose();
        mod.OnDispose();
        Check(true, "Cleanup before or after failed loading does not throw");
    }

    private static void SettingsCoverage()
    {
        // Exact keys independently extracted from the six installed mods' locale
        // dictionaries and Game.ModSetting's ID construction, including binding aliases.
        using (Stream stream = typeof(TestMain).Assembly.GetManifestResourceStream(
            "PlacemakerChinese.Tests.RequiredSettingsKeys.txt"))
        using (StreamReader reader = new StreamReader(stream))
        {
            int count = 0;
            string key;
            while ((key = reader.ReadLine()) != null)
            {
                if (key.Length == 0) continue;
                string value;
                Check(translations.TryGetValue(key, out value) &&
                    System.Text.RegularExpressions.Regex.IsMatch(value, @"[\u4e00-\u9fff]"),
                    "Chinese setting, dropdown, warning or description: " + key);
                count++;
            }
            Check(count == 175, "The six-mod reference inventory retains all 175 keys");
        }
        foreach (string action in new[] { "TriggerPreviousProfileToggle", "TriggerNextProfileToggle" })
            Check(translations["Options.OPTION[TimeWeatherAnarchy.TimeWeatherAnarchy.Mod/" + action + "/binding]"] ==
                translations["Options.OPTION[TimeWeatherAnarchy.TimeWeatherAnarchy.Mod.TimeWeatherAnarchySettings." + action + "]"],
                "Both forms of the profile hotkey resolve to the same Chinese label");
        string description = translations[
            "Options.OPTION_DESCRIPTION[ExtraLandscapingTools.ExtraLandscapingTools.ELT.ELTSettings.RegenMode]"];
        Check(description.Contains("<停用>") && description.Contains("<每日再生>") &&
            description.Contains("<无限>") && description.Contains("**性能说明**") &&
            description.Contains("\n"), "The regeneration explanation retains lists and rich-text formatting");
        MethodInfo scope = typeof(PlacemakerChinese.Mod).GetMethod("IsOwnedKey",
            BindingFlags.NonPublic | BindingFlags.Static);
        foreach (string key in new[] {
            "Options.OPTION[UnrelatedMod.Setting.RenderPrefabIcons]",
            "Options.OPTION[AssetIconLibrary.AssetIconLibrary.Mod.Setting.RenderPrefabIconsExtra]",
            "Options.OPTION[TimeWeatherAnarchy.TimeWeatherAnarchy.Mod/UnknownAction/binding]" })
            Check(!(bool)scope.Invoke(null, new object[] { key }),
                "Added settings retain exact ownership boundaries: " + key);
    }

    private static LocalizationManager Setup(string locale, bool hasSettings)
    {
        if (registration != null)
            registration.GetProperty("Settings").SetValue(null, hasSettings ? Activator.CreateInstance(settingType) : null, null);
        LocalizationManager manager = new LocalizationManager(locale);
        GameManager.instance.localizationManager = manager;
        return manager;
    }

    private static void AllChinese(LocalizationManager manager)
    {
        foreach (KeyValuePair<string, string> entry in translations)
        {
            string value;
            Check(manager.activeDictionary.TryGetValue(entry.Key, out value) && value == entry.Value,
                "Chinese value for " + entry.Key);
        }
    }

    private static Dictionary<string, string> English()
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> entry in translations)
            result[entry.Key] = "English " + entry.Key;
        result["UnrelatedMod.Title"] = "Unrelated";
        return result;
    }

    private static void SettingsChinese(LocalizationManager manager)
    {
        ModSetting settings = (ModSetting)registration.GetProperty("Settings").GetValue(null, null);
        string value;
        Check(manager.activeDictionary.TryGetValue(settings.GetOptionLabelLocaleID("ShowCursorTooltips"), out value)
            && value == translations["Placemaker.Settings.ShowCursorTooltips"], "Settings label alias");
        Check(manager.activeDictionary.TryGetValue(settings.GetOptionDescLocaleID("ShowCursorTooltips"), out value)
            && value == translations["Placemaker.Settings.ShowCursorTooltips.Desc"], "Settings description alias");
        Check(manager.activeDictionary.TryGetValue(settings.GetOptionWarningLocaleID("RemoveEverything"), out value)
            && value == translations["Placemaker.Settings.RemoveEverything.Warning"], "Settings warning alias");
        Check(manager.activeDictionary.TryGetValue(settings.GetBindingKeyLocaleID("ToggleTool"), out value)
            && value == translations["Placemaker.Settings.Action.ToggleTool"], "Shortcut alias");
        Check(manager.activeDictionary.TryGetValue("Options.Placemaker.Mod.PREVIEWDETAIL[Detailed]", out value)
            && value == translations["Placemaker.Settings.PreviewDetail.Detailed"], "Enum alias");
    }

    private static void NoOriginal()
    {
        LocalizationManager manager = Setup("zh-HANS", false);
        PlacemakerChinese.Mod mod = CreateMod();
        mod.OnLoad(null);
        AllChinese(manager);
        Check(manager.SourceCount("zh-HANS") == 1, "One initial source");
        mod.OnDispose();
        Check(manager.SourceCount("zh-HANS") == 0, "Source removed without original mod");
    }

    private static void OriginalFirst()
    {
        LocalizationManager manager = Setup("zh-HANS", true);
        manager.AddSource("zh-HANS", new MemorySource(English()));
        PlacemakerChinese.Mod mod = CreateMod();
        mod.OnLoad(null);
        AllChinese(manager);
        SettingsChinese(manager);
        Check(manager.activeDictionary.Entries["UnrelatedMod.Title"] == "Unrelated", "Other mod remains intact");
        mod.OnDispose();
    }

    private static void TranslationFirst()
    {
        LocalizationManager manager = Setup("zh-HANS", false);
        PlacemakerChinese.Mod mod = CreateMod();
        mod.OnLoad(null);
        registration.GetProperty("Settings").SetValue(null, Activator.CreateInstance(settingType), null);
        manager.AddSource("zh-HANS", new MemorySource(English()));
        AllChinese(manager);
        SettingsChinese(manager);
        int adds = manager.SourceAdds;
        for (int index = 0; index < 5; index++)
        {
            manager.SetActiveLocale("zh-HANS");
            manager.SupportedLocalesChanged();
        }
        Check(manager.SourceAdds == adds, "Refresh does not accumulate redundant sources");
        mod.OnDispose();
    }

    private static void LanguageSwitch()
    {
        LocalizationManager manager = Setup("en-US", false);
        PlacemakerChinese.Mod mod = CreateMod();
        mod.OnLoad(null);
        registration.GetProperty("Settings").SetValue(null, Activator.CreateInstance(settingType), null);
        manager.AddSource("zh-HANS", new MemorySource(English()));
        manager.AddSource("en-US", new MemorySource(English()));
        Check(manager.activeDictionary.Entries["Placemaker.Name"] == "English Placemaker.Name", "English preserved");
        manager.SetActiveLocale("zh-HANS");
        AllChinese(manager);
        SettingsChinese(manager);
        manager.SetActiveLocale("en-US");
        Check(manager.activeDictionary.Entries["Placemaker.Name"] == "English Placemaker.Name", "Return to English");
        manager.SetActiveLocale("zh-HANS");
        AllChinese(manager);
        mod.OnDispose();
    }

    private static void DeferredEvents()
    {
        LocalizationManager manager = Setup("zh-HANS", false);
        manager.DeferredEvents = true;
        PlacemakerChinese.Mod mod = CreateMod();
        mod.OnLoad(null);
        registration.GetProperty("Settings").SetValue(null, Activator.CreateInstance(settingType), null);
        manager.AddSource("zh-HANS", new MemorySource(English()));
        manager.Flush();
        AllChinese(manager);
        SettingsChinese(manager);
        int adds = manager.SourceAdds;
        manager.Flush();
        manager.SetActiveLocale("zh-HANS");
        manager.Flush();
        Check(manager.SourceAdds == adds, "Deferred events are stable");
        mod.OnDispose();
        manager.Flush();
        Check(manager.SourceCount("zh-HANS") == 1, "Original source remains after dispose");
    }

    private static void Unload()
    {
        LocalizationManager manager = Setup("zh-HANS", false);
        PlacemakerChinese.Mod mod = CreateMod();
        mod.OnLoad(null);
        manager.AddSource("zh-HANS", new MemorySource(English()));
        AllChinese(manager);
        mod.OnDispose();
        Check(manager.SourceCount("zh-HANS") == 1, "Only original source remains");
        Check(manager.activeDictionary.Entries["Placemaker.Name"] == "English Placemaker.Name", "Original text restored");
        int adds = manager.SourceAdds;
        manager.SetActiveLocale("en-US");
        manager.SetActiveLocale("zh-HANS");
        manager.SupportedLocalesChanged();
        Check(manager.SourceAdds == adds, "Disposed handlers do not run");
        manager.RebuildLocaleCatalog();
        Check(manager.activeDictionary.Entries["Placemaker.Name"] == "English Placemaker.Name",
            "Disposed translations stay absent after the game rebuilds its locale catalog");
        mod.OnDispose();
    }
}
