﻿using Digimezzo.Foundation.Core.Helpers;
using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using Knowte.Core.Base;
using Knowte.Core.IO;
using Knowte.Services.Contracts.Appearance;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Linq;


namespace Knowte.Services.Appearance
{
    public class AppearanceService : IAppearanceService
    {
        private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320;

        private FileSystemWatcher colorSchemeWatcher;
        private Timer colorSchemeTimer = new Timer();
        private string colorSchemesSubDirectory;
        private double colorSchemeTimeoutSeconds = 0.2;
        private bool followWindowsColor = false;
        private List<ColorScheme> colorSchemes = new List<ColorScheme>();
        private ColorScheme[] builtInColorSchemes = {
                                                        new ColorScheme {
                                                            Name = "Default",
                                                            AccentColor = "#1D7DD4"
                                                        },
                                                        new ColorScheme {
                                                            Name = "Green",
                                                            AccentColor = "#7FB718"
                                                        },
                                                        new ColorScheme {
                                                            Name = "Yellow",
                                                            AccentColor = "#F09609"
                                                        },
                                                        new ColorScheme {
                                                            Name = "Purple",
                                                            AccentColor = "#A835B2"
                                                        },
                                                        new ColorScheme {
                                                            Name = "Pink",
                                                            AccentColor = "#CE0058"
                                                        }
                                                    };

        public AppearanceService() : base()
        {
            // Initialize the ColorSchemes directory
            this.colorSchemesSubDirectory = Path.Combine(SettingsClient.ApplicationFolder(), ApplicationPaths.ColorSchemesSubDirectory);

            // If the ColorSchemes subdirectory doesn't exist, create it
            if (!Directory.Exists(this.colorSchemesSubDirectory))
            {
                Directory.CreateDirectory(Path.Combine(this.colorSchemesSubDirectory));
            }

            // Create the example ColorScheme
            string exampleColorSchemeFile = Path.Combine(this.colorSchemesSubDirectory, "Example.xml");

            if (File.Exists(exampleColorSchemeFile))
            {
                File.Delete(exampleColorSchemeFile);
            }

            XDocument exampleColorSchemeXml = XDocument.Parse("<ColorScheme><AccentColor>#E31837</AccentColor></ColorScheme>");
            exampleColorSchemeXml.Save(exampleColorSchemeFile);

            // Create the "How to create ColorSchemes.txt" file
            string howToFile = Path.Combine(this.colorSchemesSubDirectory, "How to create ColorSchemes.txt");

            if (File.Exists(howToFile))
            {
                File.Delete(howToFile);
            }

            string[] lines = {
                                "How to create ColorSchemes?",
                                "---------------------------",
                                "",
                                "1. Copy and rename the file Example.xml",
                                "2. Open the file and edit the color code of AccentColor",
                                "3. Your ColorScheme appears automatically in " + ProductInformation.ApplicationName
                                };

            File.WriteAllLines(howToFile, lines, System.Text.Encoding.UTF8);

            // Configure the ColorSchemeTimer
            this.colorSchemeTimer.Interval = TimeSpan.FromSeconds(this.colorSchemeTimeoutSeconds).TotalMilliseconds;
            this.colorSchemeTimer.Elapsed += new ElapsedEventHandler(ColorSchemeTimerElapsed);

            // Start the ColorSchemeWatcher
            this.colorSchemeWatcher = new FileSystemWatcher(this.colorSchemesSubDirectory);
            this.colorSchemeWatcher.EnableRaisingEvents = true;

            this.colorSchemeWatcher.Changed += new FileSystemEventHandler(WatcherChangedHandler);
            this.colorSchemeWatcher.Deleted += new FileSystemEventHandler(WatcherChangedHandler);
            this.colorSchemeWatcher.Created += new FileSystemEventHandler(WatcherChangedHandler);
            this.colorSchemeWatcher.Renamed += new RenamedEventHandler(WatcherRenamedHandler);

            // Get the available ColorSchemes
            this.GetAllColorSchemes();
        }
        
        public event EventHandler ColorSchemeChanged = delegate { };
        public event EventHandler ColorSchemesChanged = delegate { };

        [DebuggerHidden]
        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!this.followWindowsColor) return IntPtr.Zero;

            if (msg == WM_DWMCOLORIZATIONCOLORCHANGED)
            {
                this.ApplyColorScheme(string.Empty, this.followWindowsColor);
            }

            return IntPtr.Zero;
        }

        private void WatcherChangedHandler(object sender, FileSystemEventArgs e)
        {
            // Using a Timer here prevents that consecutive WatcherChanged events trigger multiple ColorSchemesChanged events
            this.colorSchemeTimer.Stop();
            this.colorSchemeTimer.Start();
        }

        private void WatcherRenamedHandler(object sender, EventArgs e)
        {
            // Using a Timer here prevents that consecutive WatcherRenamed events trigger multiple ColorSchemesChanged events
            this.colorSchemeTimer.Stop();
            this.colorSchemeTimer.Start();
        }

        private void ColorSchemeTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.colorSchemeTimer.Stop();
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.GetAllColorSchemes();
                this.OnColorSchemesChanged(new EventArgs());
            });
        }

        private void ClearColorSchemes()
        {
            this.colorSchemes.Clear();
        }

        private void GetBuiltInColorSchemes()
        {
            // For now, we are returning a hard-coded list of themes
            foreach (ColorScheme cs in this.builtInColorSchemes)
            {
                this.colorSchemes.Add(cs);
            }
        }

        private void GetCustomColorSchemes()
        {
            var dirInfo = new DirectoryInfo(this.colorSchemesSubDirectory);

            foreach (FileInfo fileInfo in dirInfo.GetFiles("*.xml"))
            {
                // We put everything in a try catch, because the user might have made errors in his XML
                try
                {
                    XDocument doc = XDocument.Load(fileInfo.FullName);
                    string colorSchemeName = fileInfo.Name.Replace(".xml", "");
                    var colorSchemeElement = (from t in doc.Elements("ColorScheme")
                                              select t).Single();

                    var colorScheme = new ColorScheme
                    {
                        Name = colorSchemeName,
                        AccentColor = colorSchemeElement.Element("AccentColor").Value
                    };

                    try
                    {
                        // This is some sort of validation of the XML file. If the conversion from String to Color doesn't work, the XML is invalid.
                        Color accentColor = (Color)ColorConverter.ConvertFromString(colorScheme.AccentColor);
                        this.AddColorScheme(colorScheme);
                    }
                    catch (Exception ex)
                    {
                        LogClient.Error("Exception: {0}", ex.Message);
                    }

                }
                catch (Exception ex)
                {
                    LogClient.Error("Exception: {0}", ex.Message);
                }
            }
        }

        protected void GetAllColorSchemes()
        {
            this.ClearColorSchemes();
            this.GetBuiltInColorSchemes();
            this.GetCustomColorSchemes();
        }

        public void OnColorSchemeChanged(EventArgs e)
        {
            this.ColorSchemeChanged(this, e);
        }

        public void OnColorSchemesChanged(EventArgs e)
        {
            this.ColorSchemesChanged(this, e);
        }

        private void AddColorScheme(ColorScheme colorScheme)
        {
            // We don't allow duplicate color scheme names.
            // If already present, remove the previous color scheme which has that name.
            // This allows custom color schemes to override built-in color schemes.
            if (this.colorSchemes.Contains(colorScheme))
            {
                this.colorSchemes.Remove(colorScheme);
            }

            this.colorSchemes.Add(colorScheme);
        }

        public ColorScheme GetColorScheme(string name)
        {
            foreach (ColorScheme item in this.colorSchemes)
            {
                if (item.Name.Equals(name))
                {
                    return item;
                }
            }

            // If not found by the loop, return the first color scheme.
            return this.colorSchemes[0];
        }

        public List<ColorScheme> GetColorSchemes()
        {
            return this.colorSchemes.ToList();
        }

        public void WatchWindowsColor(object win)
        {
            if (win is Window)
            {
                IntPtr windowHandle = (new WindowInteropHelper((Window)win)).Handle;
                HwndSource src = HwndSource.FromHwnd(windowHandle);
                src.AddHook(new HwndSourceHook(WndProc));
            }
        }

        private void ApplyAccentColor(Color accentColor)
        {
            Application.Current.Resources["Color_PrimaryAccent"] = accentColor;
            Application.Current.Resources["Brush_PrimaryAccent"] = new SolidColorBrush(accentColor);

            // Re-apply theme to ensure brushes referencing AccentColor are updated
            this.ReApplyTheme();
            this.OnColorSchemeChanged(new EventArgs());
        }

        private ResourceDictionary GetCurrentThemeDictionary()
        {
            // Determine the current theme by looking at the application resources and return 
            // the first dictionary having the resource key 'Brush_AppearanceService' defined.
            return (from dict in Application.Current.Resources.MergedDictionaries
                    where dict.Contains("Brush_AppearanceService")
                    select dict).FirstOrDefault();
        }

        private void ReApplyTheme()
        {
            ResourceDictionary currentThemeDict = this.GetCurrentThemeDictionary();

            if (currentThemeDict != null)
            {
                var newThemeDict = new ResourceDictionary { Source = currentThemeDict.Source };

                // Prevent exceptions by adding the new dictionary before removing the old one
                Application.Current.Resources.MergedDictionaries.Add(newThemeDict);
                Application.Current.Resources.MergedDictionaries.Remove(currentThemeDict);
            }
        }

        private Color InterpolateColor(Color from, Color to, double progress)
        {
            return from + ((to - from) * (float)progress);
        }

        private async void InterpolateAccentColorAsync(Color accentColor)
        {
            await Task.Run(async() =>
            {
                int loop = 0;
                int loopMax = 30;
                var oldColor = (Color)Application.Current.Resources["Color_PrimaryAccent"];

                while (loop < loopMax)
                {
                    loop++;
                    Color color = this.InterpolateColor(oldColor, accentColor, loop / (double)loopMax);

                    Application.Current.Resources["Color_PrimaryAccent"] = color;
                    Application.Current.Resources["Brush_PrimaryAccent"] = new SolidColorBrush(color);
                    await Task.Delay(7);
                }
            });

            // Re-apply theme to ensure brushes referencing AccentColor are updated
            this.ReApplyTheme();
            this.OnColorSchemeChanged(new EventArgs());
        }

        private string GetWindowsDWMColor()
        {
            string returnColor = string.Empty;

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\DWM");

                if (key != null)
                {
                    object value = null;

                    value = key.GetValue("ColorizationColor");

                    Color color = (Color)ColorConverter.ConvertFromString(string.Format("#{0:X6}", value));

                    // We trim the first 3 characters: # and the 2 characters indicating opacity. We want opacity=1.0
                    string trimmedColor = "#" + color.ToString().Substring(3);

                    returnColor = trimmedColor;
                }
            }
            catch (Exception)
            {
            }

            return returnColor;
        }

        public void ApplyColorScheme(string selectedColorScheme, bool followWindowsColor)
        {
            this.followWindowsColor = followWindowsColor;

            Color accentColor = default(Color);
            bool applySelectedColorScheme = false;

            try
            {
                if (followWindowsColor)
                {
                    accentColor = (Color)ColorConverter.ConvertFromString(this.GetWindowsDWMColor());
                }
                else
                {
                    applySelectedColorScheme = true;
                }
            }
            catch (Exception)
            {
                applySelectedColorScheme = true;
            }

            if (applySelectedColorScheme)
            {
                ColorScheme cs = this.GetColorScheme(selectedColorScheme);
                accentColor = (Color)ColorConverter.ConvertFromString(cs.AccentColor);
            }
            else
            {
                accentColor = HSLColor.Normalize(accentColor, 30);
            }

            if (Application.Current.Resources["Color_PrimaryAccent"] == null)
            {
                this.ApplyAccentColor(accentColor);
                return;
            }

            this.InterpolateAccentColorAsync(accentColor);
        }
    }
}
