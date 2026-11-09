using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace WinMine.Controls;

/// <summary>
/// Fluent motion for the tiles: a revealed tile settles in (fade + a whisper of scale), a flag pops,
/// a lost mine pulses. Everything runs on the compositor, so a 400-cell flood stays at frame rate and
/// needs no dependent animation.
/// </summary>
/// <remarks>
/// The animated element must not use <see cref="UIElement.CenterPoint"/>: WinUI refuses to hand out a
/// visual for such an element ("GetElementVisual API is not allowed on this object at this time, as this
/// object currently has the CenterPoint property in use"). The tile therefore wraps its content in a clean
/// child (<c>CellVisual.Surface</c>) and the centre is set on the composition visual instead.
/// </remarks>
internal static class CellMotion
{
    private const int RevealScaleMs = 180;
    private const int TextPopMs = 150;
    private const int TextDelayMs = 30;
    private const int FlagPopMs = 160;
    private const int ExplodeMs = 340;

    // Fluent standard easing: cubic-bezier(0.1, 0.9, 0.2, 1.0).
    private static readonly Dictionary<Compositor, CubicBezierEasingFunction> Easings = new();

    /// <summary>How many tile animations were started successfully (used by --selftest).</summary>
    internal static int Started { get; private set; }

    /// <summary>First failure seen while starting an animation, if any (used by --selftest).</summary>
    internal static string? LastFailure { get; private set; }

    /// <summary>Which motion call recorded <see cref="LastFailure"/>, for diagnostics.</summary>
    internal static string? LastStage { get; private set; }

    /// <summary>
    /// Properties the last reveal touched. The tile must never fade (a big flood would blink the whole
    /// board), so this is expected to stay free of "opacity"; --selftest guards that.
    /// </summary>
    internal static string LastRevealProperties { get; private set; } = string.Empty;

    /// <summary>Stagger for a reveal wave; capped so a 400-cell flood keeps its whole ripple under ~200 ms.</summary>
    internal static int RevealDelay(int wave) => Math.Clamp(wave, 0, 12) * 16;

    private static CubicBezierEasingFunction Ease(Compositor compositor)
    {
        if (Easings.TryGetValue(compositor, out var cached))
        {
            return cached;
        }

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
        Easings[compositor] = easing;
        return easing;
    }

    private static Vector3 Centre(float half) => new(half, half, 0f);

    /// <summary>
    /// A revealed tile settles into place; <paramref name="delayMs"/> makes floods ripple outwards.
    /// Deliberately does <em>not</em> fade the tile in: a first click can open a hundred tiles at once,
    /// and fading them all from zero reads as the board blinking. The tile therefore keeps full opacity
    /// and only scales, while the number on top pops in.
    /// </summary>
    public static void Reveal(UIElement element, UIElement? text, float half, int delayMs)
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var easing = Ease(compositor);

            visual.CenterPoint = Centre(half);
            visual.StopAnimation("Opacity");
            visual.StopAnimation("Scale");
            visual.Opacity = 1f;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0f, new Vector3(0.93f, 0.93f, 1f));
            scale.InsertKeyFrame(1f, Vector3.One, easing);
            scale.Duration = TimeSpan.FromMilliseconds(RevealScaleMs);
            scale.DelayTime = TimeSpan.FromMilliseconds(delayMs);
            visual.StartAnimation("Scale", scale);

            if (text is not null && text is FrameworkElement framework && framework.ActualWidth > 0)
            {
                var textVisual = ElementCompositionPreview.GetElementVisual(text);
                textVisual.CenterPoint = new Vector3(
                    (float)(framework.ActualWidth / 2),
                    (float)(framework.ActualHeight / 2),
                    0f);
                textVisual.StopAnimation("Opacity");
                textVisual.StopAnimation("Scale");

                var fade = compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0f, 0f);
                fade.InsertKeyFrame(1f, 1f, easing);
                fade.Duration = TimeSpan.FromMilliseconds(TextPopMs);
                fade.DelayTime = TimeSpan.FromMilliseconds(delayMs + TextDelayMs);
                textVisual.StartAnimation("Opacity", fade);

                var pop = compositor.CreateVector3KeyFrameAnimation();
                pop.InsertKeyFrame(0f, new Vector3(0.74f, 0.74f, 1f));
                pop.InsertKeyFrame(1f, Vector3.One, easing);
                pop.Duration = TimeSpan.FromMilliseconds(TextPopMs);
                pop.DelayTime = TimeSpan.FromMilliseconds(delayMs + TextDelayMs);
                textVisual.StartAnimation("Scale", pop);
            }

            LastRevealProperties = text is null ? "scale" : "scale,text";
            Started++;
        }
        catch (Exception ex)
        {
            LastFailure = ex.GetType().Name + ": " + ex.Message;
            LastStage = "reveal";
            // Motion is decoration: never let it break a move.
        }
    }

    /// <summary>A just-placed flag pops in with a short overshoot.</summary>
    public static void Pop(UIElement? icon)
    {
        if (icon is null)
        {
            return;
        }

        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(icon);
            var compositor = visual.Compositor;
            var easing = Ease(compositor);

            // The icon is a 20x20 design box laid out centred in the tile, so its own centre is (10,10).
            visual.CenterPoint = new Vector3(10f, 10f, 0f);
            visual.StopAnimation("Scale");
            visual.StopAnimation("Opacity");

            var pop = compositor.CreateVector3KeyFrameAnimation();
            pop.InsertKeyFrame(0f, new Vector3(0.45f, 0.45f, 1f));
            pop.InsertKeyFrame(0.65f, new Vector3(1.08f, 1.08f, 1f), easing);
            pop.InsertKeyFrame(1f, Vector3.One, easing);
            pop.Duration = TimeSpan.FromMilliseconds(FlagPopMs);
            visual.StartAnimation("Scale", pop);

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, easing);
            fade.Duration = TimeSpan.FromMilliseconds(FlagPopMs - 50);
            visual.StartAnimation("Opacity", fade);

            Started++;
        }
        catch (Exception ex)
        {
            LastFailure = ex.GetType().Name + ": " + ex.Message;
            LastStage = "pop";
        }
    }

    /// <summary>The mine that ended the game pulses once.</summary>
    public static void Explode(UIElement element, float half)
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var easing = Ease(compositor);

            visual.CenterPoint = Centre(half);
            visual.StopAnimation("Scale");

            var pulse = compositor.CreateVector3KeyFrameAnimation();
            pulse.InsertKeyFrame(0f, Vector3.One);
            pulse.InsertKeyFrame(0.32f, new Vector3(1.18f, 1.18f, 1f), easing);
            pulse.InsertKeyFrame(1f, Vector3.One, easing);
            pulse.Duration = TimeSpan.FromMilliseconds(ExplodeMs);
            visual.StartAnimation("Scale", pulse);

            Started++;
        }
        catch (Exception ex)
        {
            LastFailure = ex.GetType().Name + ": " + ex.Message;
            LastStage = "explode";
        }
    }

    /// <summary>Drops any running animation so the tile can be repainted (theme change, new game, refresh).</summary>
    public static void Stop(UIElement element, float half)
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.CenterPoint = Centre(half);
            visual.StopAnimation("Opacity");
            visual.StopAnimation("Scale");
            visual.Opacity = 1f;
            visual.Scale = Vector3.One;
        }
        catch (Exception ex)
        {
            LastFailure = ex.GetType().Name + ": " + ex.Message;
            LastStage = "stop";
        }
    }
}
