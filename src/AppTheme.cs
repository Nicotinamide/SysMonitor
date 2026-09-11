using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SysMonitor
{
    public enum ThemeMode
    {
        Dark,
        Light
    }

    public class ThemePalette
    {
        public ThemeMode Mode { get; set; }

        // Backgrounds & Surfaces
        public SolidColorBrush WindowBg { get; set; }
        public SolidColorBrush CardBg { get; set; }
        public SolidColorBrush CardHoverBg { get; set; }
        public SolidColorBrush InnerTileBg { get; set; }
        public SolidColorBrush BorderBrush { get; set; }
        public SolidColorBrush BorderMuted { get; set; }
        public SolidColorBrush InputBg { get; set; }
        public SolidColorBrush InputBorder { get; set; }

        // Text Colors (High Contrast & Clear)
        public SolidColorBrush TextPrimary { get; set; }
        public SolidColorBrush TextSecondary { get; set; }
        public SolidColorBrush TextMuted { get; set; }
        public SolidColorBrush TextDim { get; set; }

        // Accents
        public SolidColorBrush AccentBlue { get; set; }
        public SolidColorBrush AccentEmerald { get; set; }
        public SolidColorBrush AccentAmber { get; set; }
        public SolidColorBrush AccentRed { get; set; }
        public SolidColorBrush ProgressBarTrack { get; set; }

        // Scrollbar & Context Menu Template Hexes
        public string ScrollThumbHex { get; set; }
        public string ScrollHoverHex { get; set; }
        public string MenuBgHex { get; set; }
        public string MenuBorderHex { get; set; }
        public string MenuHoverHex { get; set; }
        public string MenuTextHex { get; set; }
        public string ButtonHoverHex { get; set; }

        // Window Shadow
        public Color ShadowColor { get; set; }
        public double ShadowOpacity { get; set; }

        public static ThemePalette CreateDark()
        {
            ThemePalette p = new ThemePalette
            {
                Mode = ThemeMode.Dark,
                WindowBg = Freeze(new SolidColorBrush(Color.FromRgb(13, 17, 23))),          // #0d1117
                CardBg = Freeze(new SolidColorBrush(Color.FromRgb(22, 27, 34))),            // #161b22
                CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(33, 38, 45))),       // #21262d
                InnerTileBg = Freeze(new SolidColorBrush(Color.FromRgb(13, 17, 23))),       // #0d1117
                BorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(48, 54, 61))),       // #30363d
                BorderMuted = Freeze(new SolidColorBrush(Color.FromRgb(33, 38, 45))),       // #21262d
                InputBg = Freeze(new SolidColorBrush(Color.FromRgb(13, 17, 23))),           // #0d1117
                InputBorder = Freeze(new SolidColorBrush(Color.FromRgb(48, 54, 61))),       // #30363d

                // High Contrast Text
                TextPrimary = Freeze(new SolidColorBrush(Color.FromRgb(240, 246, 252))),     // #f0f6fc Pure Crisp White
                TextSecondary = Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225))),   // #cbd5e1 Light Silver White (Crystal Clear)
                TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184))),       // #94a3b8 Slate Muted
                TextDim = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139))),         // #64748b Slate Dim

                AccentBlue = Freeze(new SolidColorBrush(Color.FromRgb(88, 166, 255))),      // #58a6ff
                AccentEmerald = Freeze(new SolidColorBrush(Color.FromRgb(16, 185, 129))),   // #10b981
                AccentAmber = Freeze(new SolidColorBrush(Color.FromRgb(245, 158, 11))),     // #f59e0b
                AccentRed = Freeze(new SolidColorBrush(Color.FromRgb(248, 81, 73))),        // #f85149
                ProgressBarTrack = Freeze(new SolidColorBrush(Color.FromRgb(33, 38, 45))),  // #21262d

                ScrollThumbHex = "#484f58",
                ScrollHoverHex = "#6e7681",
                MenuBgHex = "#161b22",
                MenuBorderHex = "#30363d",
                MenuHoverHex = "#21262d",
                MenuTextHex = "#f0f6fc",
                ButtonHoverHex = "#21262d",

                ShadowColor = Colors.Black,
                ShadowOpacity = 0.55
            };
            return p;
        }

        public static ThemePalette CreateLight()
        {
            ThemePalette p = new ThemePalette
            {
                Mode = ThemeMode.Light,
                WindowBg = Freeze(new SolidColorBrush(Color.FromRgb(246, 248, 250))),       // #f6f8fa Soft Apple Light
                CardBg = Freeze(new SolidColorBrush(Color.FromRgb(255, 255, 255))),         // #ffffff Pure White Card
                CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249))),    // #f1f5f9
                InnerTileBg = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252))),    // #f8fafc
                BorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(208, 215, 222))),    // #d0d7de
                BorderMuted = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240))),    // #e2e8f0
                InputBg = Freeze(new SolidColorBrush(Color.FromRgb(255, 255, 255))),        // #ffffff
                InputBorder = Freeze(new SolidColorBrush(Color.FromRgb(208, 215, 222))),    // #d0d7de

                // High Contrast Text for Light Background
                TextPrimary = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42))),        // #0f172a Deep Slate Black
                TextSecondary = Freeze(new SolidColorBrush(Color.FromRgb(51, 65, 85))),      // #334155 Slate 700 (Very Legible)
                TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139))),       // #64748b Slate 500
                TextDim = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184))),         // #94a3b8 Slate 400

                AccentBlue = Freeze(new SolidColorBrush(Color.FromRgb(9, 105, 218))),       // #0969da
                AccentEmerald = Freeze(new SolidColorBrush(Color.FromRgb(5, 150, 105))),    // #059669
                AccentAmber = Freeze(new SolidColorBrush(Color.FromRgb(217, 119, 6))),      // #d97706
                AccentRed = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38))),        // #dc2626
                ProgressBarTrack = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240))), // #e2e8f0

                ScrollThumbHex = "#cbd5e1",
                ScrollHoverHex = "#94a3b8",
                MenuBgHex = "#ffffff",
                MenuBorderHex = "#d0d7de",
                MenuHoverHex = "#f1f5f9",
                MenuTextHex = "#0f172a",
                ButtonHoverHex = "#e2e8f0",

                ShadowColor = Color.FromRgb(100, 116, 139),
                ShadowOpacity = 0.22
            };
            return p;
        }

        private static SolidColorBrush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }

    public static class AppTheme
    {
        private static ThemePalette _current = ThemePalette.CreateLight();

        public static ThemePalette Current
        {
            get { return _current; }
        }

        public static bool IsDark
        {
            get { return _current.Mode == ThemeMode.Dark; }
        }

        public static void SetTheme(ThemeMode mode)
        {
            _current = mode == ThemeMode.Light ? ThemePalette.CreateLight() : ThemePalette.CreateDark();
        }

        // =========================================================================
        // 1. Modern Slim ScrollBar Style (Eliminates classic Windows white scrollbar)
        // =========================================================================
        public static void ApplyModernScrollBarStyle(ScrollViewer sv)
        {
            if (sv == null) return;
            string xaml = string.Format(
@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        TargetType='{{x:Type ScrollBar}}'>
    <Setter Property='Width' Value='6'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='{{x:Type ScrollBar}}'>
                <Grid Background='Transparent'>
                    <Track x:Name='PART_Track' IsDirectionReversed='True'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Command='{{x:Static ScrollBar.PageUpCommand}}' Opacity='0' Focusable='False'/>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command='{{x:Static ScrollBar.PageDownCommand}}' Opacity='0' Focusable='False'/>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='{{x:Type Thumb}}'>
                                        <Border x:Name='ThumbBorder' CornerRadius='3' Background='{0}' Margin='1,0,1,0'/>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property='IsMouseOver' Value='True'>
                                                <Setter TargetName='ThumbBorder' Property='Background' Value='{1}'/>
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>", _current.ScrollThumbHex, _current.ScrollHoverHex);

            Style scrollBarStyle = (Style)XamlReader.Parse(xaml);
            sv.Resources[typeof(ScrollBar)] = scrollBarStyle;
        }

        // =========================================================================
        // 2. Modern ContextMenu (Eliminates classic Windows icon gutter white strip)
        // =========================================================================
        public static ContextMenu CreateModernContextMenu()
        {
            ContextMenu cm = new ContextMenu();
            cm.SnapsToDevicePixels = true;
            cm.OverridesDefaultStyle = true;

            string xaml = string.Format(
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='{{x:Type ContextMenu}}'>
    <Border Background='{0}' BorderBrush='{1}' BorderThickness='1' CornerRadius='8' Padding='4'>
        <Border.Effect>
            <DropShadowEffect BlurRadius='16' ShadowDepth='4' Opacity='{2}' Color='{3}'/>
        </Border.Effect>
        <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
    </Border>
</ControlTemplate>", _current.MenuBgHex, _current.MenuBorderHex, _current.ShadowOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture), _current.Mode == ThemeMode.Dark ? "Black" : "#64748b");

            cm.Template = (ControlTemplate)XamlReader.Parse(xaml);
            return cm;
        }

        public static MenuItem CreateModernMenuItem(string header, RoutedEventHandler onClick, bool isChecked = false)
        {
            MenuItem mi = new MenuItem();
            mi.Header = header;
            if (onClick != null) mi.Click += onClick;

            string xaml = string.Format(
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='{{x:Type MenuItem}}'>
    <Border x:Name='Bd' Background='Transparent' CornerRadius='6' Padding='10,6,10,6' Margin='0,1,0,1' Cursor='Hand'>
        <ContentPresenter ContentSource='Header' RecognizesAccessKey='True'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsHighlighted' Value='True'>
            <Setter TargetName='Bd' Property='Background' Value='{0}'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>", _current.MenuHoverHex);

            mi.Template = (ControlTemplate)XamlReader.Parse(xaml);
            mi.Foreground = _current.TextPrimary;
            mi.FontSize = 11.5;
            mi.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");

            if (isChecked)
            {
                mi.FontWeight = FontWeights.SemiBold;
                mi.Foreground = _current.AccentBlue;
            }

            return mi;
        }

        public static Separator CreateModernSeparator()
        {
            Separator sep = new Separator();
            string xaml = string.Format(
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='{{x:Type Separator}}'>
    <Border Height='1' Background='{0}' Margin='6,3,6,3'/>
</ControlTemplate>", _current.MenuBorderHex);

            sep.Template = (ControlTemplate)XamlReader.Parse(xaml);
            return sep;
        }

        // =========================================================================
        // 3. Modern Icon Button (Eliminates classic Windows button blue/white box)
        // =========================================================================
        public static Button CreateIconButton(string symbol, string tooltip, RoutedEventHandler onClick, double fontSize = 11)
        {
            Button btn = new Button();
            btn.Content = symbol;
            btn.ToolTip = tooltip;
            btn.Foreground = _current.TextSecondary;
            btn.FontSize = fontSize;
            btn.Cursor = Cursors.Hand;
            btn.VerticalAlignment = VerticalAlignment.Center;
            btn.Padding = new Thickness(4, 2, 4, 2);

            string xaml = string.Format(
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='{{x:Type Button}}'>
    <Border x:Name='Bd' Background='Transparent' CornerRadius='4' Padding='{{TemplateBinding Padding}}'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Bd' Property='Background' Value='{0}'/>
        </Trigger>
        <Trigger Property='IsPressed' Value='True'>
            <Setter TargetName='Bd' Property='Opacity' Value='0.7'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>", _current.ButtonHoverHex);

            btn.Template = (ControlTemplate)XamlReader.Parse(xaml);
            if (onClick != null) btn.Click += onClick;
            return btn;
        }
    }
}
