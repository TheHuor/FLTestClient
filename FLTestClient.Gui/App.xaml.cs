using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FLTestClient.Gui;

/// <summary>Application; switches theme (palette Dark/Bright) and language (en/de) at runtime.</summary>
public partial class App : Application
{
    private const int PaletteSlot = 0;
    private const int LanguageSlot = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Swaps the colour palette. All styles use DynamicResource and update immediately; the title bar of the
    /// open windows is set dark or light to match.
    /// </summary>
    /// <param name="dark">True for Dark.xaml, otherwise Bright.xaml.</param>
    public static void ApplyTheme(bool dark)
    {
        Current.Resources.MergedDictionaries[PaletteSlot] =
            new ResourceDictionary { Source = new Uri($"/FLTestClientGui;component/Themes/{(dark ? "Dark" : "Bright")}.xaml", UriKind.Relative) };
        foreach (Window w in Current.Windows)
            SetDarkTitleBar(w, dark);
    }

    /// <summary>
    /// Swaps the UI language and sets the core language (<see cref="Texts.Language"/>) for bot log lines.
    /// English stays merged underneath, so missing translations fall back to English.
    /// </summary>
    /// <param name="language"><c>en</c> or <c>de</c>; anything else means English.</param>
    public static void ApplyLanguage(string language)
    {
        Texts.Language = language;
        Current.Resources.MergedDictionaries[LanguageSlot] =
            new ResourceDictionary { Source = new Uri($"/FLTestClientGui;component/Languages/{Texts.Language}.xaml", UriKind.Relative) };
    }

    /// <summary>Sets the title bar of a window dark or light (Windows 10 20H1 and later).</summary>
    /// <param name="window">Window with a valid handle.</param>
    /// <param name="dark">True for a dark title bar.</param>
    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        int value = dark ? 1 : 0;
        // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
    }
}

/// <summary>Looks up UI texts of the active language for code (view models).</summary>
public static class Loc
{
    /// <summary>Returns a translated, formatted UI text.</summary>
    /// <param name="key">Key from <c>Languages/en.xaml</c>, e.g. <c>Msg.Ready</c>.</param>
    /// <param name="args">Values for the placeholders.</param>
    /// <returns>The text; the key itself if it is unknown.</returns>
    public static string T(string key, params object?[] args)
    {
        string format = Application.Current?.TryFindResource(key) as string ?? key;
        return args.Length == 0 ? format : string.Format(Texts.Culture, format, args);
    }
}
