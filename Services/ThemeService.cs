using System.Windows;
using System.Windows.Media;

namespace FloatNote.Services;

public static class ThemeService
{
    public static void Apply(bool isDarkTheme)
    {
        var resources = System.Windows.Application.Current.Resources;

        if (isDarkTheme)
        {
            Set(resources, "WindowBackgroundBrush", "#FF171A1D");
            Set(resources, "PanelBackgroundBrush", "#FF20252A");
            Set(resources, "AccentBrush", "#FF70D7A9");
            Set(resources, "AccentSoftBrush", "#FF263B35");
            Set(resources, "TextBrush", "#FFF1F4F2");
            Set(resources, "MutedTextBrush", "#FFADB8B2");
            Set(resources, "InputBackgroundBrush", "#FF111417");
            Set(resources, "BorderBrush", "#FF3A443F");
            Set(resources, "ButtonBackgroundBrush", "#FF263039");
            return;
        }

        Set(resources, "WindowBackgroundBrush", "#FFF9F2E8");
        Set(resources, "PanelBackgroundBrush", "#FFFFFCF7");
        Set(resources, "AccentBrush", "#FF2A7F62");
        Set(resources, "AccentSoftBrush", "#FFE0F0E9");
        Set(resources, "TextBrush", "#FF252525");
        Set(resources, "MutedTextBrush", "#FF74706A");
        Set(resources, "InputBackgroundBrush", "#FFFFFFFF");
        Set(resources, "BorderBrush", "#FFD8CEC0");
        Set(resources, "ButtonBackgroundBrush", "#FFFFFFFF");
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }
}
