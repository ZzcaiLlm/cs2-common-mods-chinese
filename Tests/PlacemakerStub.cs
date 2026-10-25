namespace Placemaker.Game.Settings
{
    public enum PreviewDetail { Simple, Normal, Detailed }
    public class PlacemakerSettings : global::Game.Modding.ModSetting
    {
        public bool ShowCursorTooltips { get; set; }
        public bool RemoveEverything { get; set; }
        public bool ResetDefaults { get; set; }
        public PreviewDetail PreviewDetail { get; set; }
    }
}

namespace Placemaker.Game.Tool
{
    public static class ToolRegistration
    {
        public static Placemaker.Game.Settings.PlacemakerSettings Settings { get; set; }
    }
}
