using BoltFramePlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace BoltFramePlugin.Services
{
    //public static class ResourceManager
    //{
    //    private static readonly Dictionary<Theme, Uri> _themeDictionaryUris = new Dictionary<Theme, Uri>
    //    {
    //        { Theme.Light, new Uri("/BoltFramePlugin;component/Themes/LightTheme.xaml", UriKind.Relative) },
    //        { Theme.Dark, new Uri("/BoltFramePlugin;component/Themes/DarkTheme.xaml", UriKind.Relative) }
    //    };

    //    private static ResourceDictionary _currentThemeDictionary;

    //    /// <summary>
    //    /// Applies the specified theme by merging its ResourceDictionary.
    //    /// </summary>
    //    /// <param name="theme">The theme to apply.</param>
    //    public static void ApplyTheme(Theme theme)
    //    {
    //        if (!_themeDictionaryUris.ContainsKey(theme))
    //            throw new ArgumentException($"Theme '{theme}' is not defined.");

    //        Uri themeUri = _themeDictionaryUris[theme];
    //        ResourceDictionary newTheme = new ResourceDictionary { Source = themeUri };

    //        // Remove the current theme dictionary if it exists
    //        if (_currentThemeDictionary != null)
    //        {
    //            Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
    //        }

    //        // Add the new theme dictionary
    //        Application.Current.Resources.MergedDictionaries.Add(newTheme);
    //        _currentThemeDictionary = newTheme;
    //    }

    //    /// <summary>
    //    /// Gets the current applied theme.
    //    /// </summary>
    //    public static Theme CurrentTheme { get; private set; } = Theme.Light;

    //    /// <summary>
    //    /// Switches the theme to the specified one.
    //    /// </summary>
    //    /// <param name="theme">The theme to switch to.</param>
    //    public static void SwitchTheme(Theme theme)
    //    {
    //        if (theme != CurrentTheme)
    //        {
    //            ApplyTheme(theme);
    //            CurrentTheme = theme;
    //        }
    //    }
    //}
}
