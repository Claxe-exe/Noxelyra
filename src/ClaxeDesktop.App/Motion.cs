using System.Windows;
using System.Windows.Media.Animation;
namespace ClaxeDesktop.App;

/// <summary>One motion vocabulary for every surface: decelerating ease, short durations, and a single off switch.</summary>
internal static class Motion
{
    private static readonly IEasingFunction Arrive = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };
    /// <summary>Respects the app setting, Windows "animation effects" and high contrast.</summary>
    internal static bool Enabled(DesktopViewModel model) => model.Settings.AnimationsEnabled && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    internal static void To(IAnimatable target, DependencyProperty property, double value, int milliseconds) =>
        target.BeginAnimation(property, new DoubleAnimation(value, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = Arrive }, HandoffBehavior.SnapshotAndReplace);
}
