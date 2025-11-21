using LoBIM.Models;
using System;
using System.Linq;
using System.Windows;
using Application = System.Windows.Application;

namespace LoBIM.Services
{
    public interface IThemeService
    {
        void SetTheme(Theme theme);
        Theme CurrentTheme { get; }
    }

    public class ThemeService : IThemeService
    {
        public Theme CurrentTheme { get; private set; }

        public ThemeService()
        {
            // Initialize with default theme
            CurrentTheme = Theme.Light;
            //ResourceManager.ApplyTheme(CurrentTheme);
        }

        public void SetTheme(Theme theme)
        {
            if (theme == CurrentTheme)
                return;

            //ResourceManager.ApplyTheme(theme);
            CurrentTheme = theme;
        }
    }
}
