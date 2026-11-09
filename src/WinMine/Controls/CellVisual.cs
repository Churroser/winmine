using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using WinMine.Core;
using Windows.Foundation;

namespace WinMine.Controls;

internal enum CellIcon
{
    None,
    Flag,
    Mine,
    WrongFlag,
}

/// <summary>One tile on the board: a rounded surface, a number and lazily created vector icons.</summary>
internal sealed class CellVisual
{
    /// <summary>Icons are authored in a 20x20 design box and scaled to the current tile size.</summary>
    private const double IconDesignSize = 20;

    private Path? _flag;
    private Path? _mine;
    private Path? _cross;
    private double _iconScale = 1;
    private double _size = 30;

    public CellVisual(Cell cell)
    {
        Cell = cell;

        Tile = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
        };

        Text = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = FontWeights.SemiBold,
        };

        // Root keeps the UIElement.CenterPoint used by the pressed-shrink effect. The reveal motion
        // animates Surface instead: GetElementVisual refuses to hand out a visual for an element that
        // has CenterPoint in use, so the animated element has to be a clean child.
        Surface = new Grid { IsHitTestVisible = false };
        Surface.Children.Add(Tile);
        Surface.Children.Add(Text);

        Root = new Grid { IsHitTestVisible = false };
        Root.Children.Add(Surface);
    }

    public Cell Cell { get; }

    public Grid Root { get; }

    /// <summary>The layer the reveal/explode motion animates (no CenterPoint of its own).</summary>
    public Grid Surface { get; }

    public Border Tile { get; }

    public TextBlock Text { get; }

    public CellIcon Icon { get; private set; }

    public void SetSize(double cellSize)
    {
        _size = cellSize;
        Root.Width = cellSize;
        Root.Height = cellSize;
        Root.CenterPoint = new System.Numerics.Vector3((float)(cellSize / 2), (float)(cellSize / 2), 0);
        Text.FontSize = Math.Round(cellSize * 0.58);
        _iconScale = Math.Round(cellSize * 0.62) / IconDesignSize;
        foreach (var path in AllIcons())
        {
            ApplyIconScale(path);
        }
    }

    public void SetIcon(CellIcon icon, Brush brush)
    {
        Icon = icon;

        // Create on demand so a fresh 30x16 board stays cheap.
        switch (icon)
        {
            case CellIcon.Flag when _flag is null:
                CreateFlag(brush);
                break;
            case CellIcon.Mine when _mine is null:
                CreateMine(brush);
                break;
            case CellIcon.WrongFlag when _cross is null:
                CreateCross(brush);
                break;
        }

        if (_flag is not null)
        {
            _flag.Visibility = icon == CellIcon.Flag ? Visibility.Visible : Visibility.Collapsed;
            _flag.Fill = brush;
            _flag.Stroke = brush;
        }

        if (_mine is not null)
        {
            _mine.Visibility = icon == CellIcon.Mine ? Visibility.Visible : Visibility.Collapsed;
            _mine.Fill = brush;
        }

        if (_cross is not null)
        {
            _cross.Visibility = icon == CellIcon.WrongFlag ? Visibility.Visible : Visibility.Collapsed;
            _cross.Stroke = brush;
        }
    }

    public void ClearIcon()
    {
        Icon = CellIcon.None;
        if (_flag is not null)
        {
            _flag.Visibility = Visibility.Collapsed;
        }

        if (_mine is not null)
        {
            _mine.Visibility = Visibility.Collapsed;
        }

        if (_cross is not null)
        {
            _cross.Visibility = Visibility.Collapsed;
        }
    }

    public void ClearText() => Text.Visibility = Visibility.Collapsed;

    /// <summary>The icon currently on show, if any (used to pop a fresh flag).</summary>
    private Path? VisibleIcon => Icon switch
    {
        CellIcon.Flag => _flag,
        CellIcon.Mine => _mine,
        CellIcon.WrongFlag => _cross,
        _ => null,
    };

    public void PlayRevealMotion(int delayMilliseconds) =>
        CellMotion.Reveal(Surface, (float)(_size / 2), delayMilliseconds);

    public void PlayFlagMotion() => CellMotion.Pop(VisibleIcon);

    public void PlayExplodeMotion() => CellMotion.Explode(Surface, (float)(_size / 2));

    public void StopMotion() => CellMotion.Stop(Surface, (float)(_size / 2));

    private void ApplyIconScale(Path path)
    {
        path.RenderTransform = new ScaleTransform { ScaleX = _iconScale, ScaleY = _iconScale };
    }

    private IEnumerable<Path> AllIcons()
    {
        if (_flag is not null)
        {
            yield return _flag;
        }

        if (_mine is not null)
        {
            yield return _mine;
        }

        if (_cross is not null)
        {
            yield return _cross;
        }
    }

    private static Path CreatePath(Brush brush) => new()
    {
        Width = IconDesignSize,
        Height = IconDesignSize,
        Stretch = Stretch.None,
        RenderTransformOrigin = new Point(0.5, 0.5),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false,
        StrokeThickness = 1.8,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        Fill = brush,
        Stroke = brush,
    };

    private void CreateFlag(Brush brush)
    {
        var geometry = new PathGeometry();

        // Solid, stroke-free geometry so the pole stays crisp at small tile sizes.
        // 旗杆 x 8.6~9.8 · 底座 x 4.2~14.2 · 三角旗面 (9.8,3.2)-(16.0,6.2)-(9.8,9.2)
        var pole = new PathFigure { StartPoint = new Point(8.6, 3.2), IsFilled = true, IsClosed = true };
        pole.Segments.Add(new LineSegment { Point = new Point(9.8, 3.2) });
        pole.Segments.Add(new LineSegment { Point = new Point(9.8, 15.6) });
        pole.Segments.Add(new LineSegment { Point = new Point(8.6, 15.6) });
        geometry.Figures.Add(pole);

        var baseBar = new PathFigure { StartPoint = new Point(4.2, 15.6), IsFilled = true, IsClosed = true };
        baseBar.Segments.Add(new LineSegment { Point = new Point(14.2, 15.6) });
        baseBar.Segments.Add(new LineSegment { Point = new Point(14.2, 17.2) });
        baseBar.Segments.Add(new LineSegment { Point = new Point(4.2, 17.2) });
        geometry.Figures.Add(baseBar);

        var cloth = new PathFigure { StartPoint = new Point(9.8, 3.2), IsFilled = true, IsClosed = true };
        cloth.Segments.Add(new LineSegment { Point = new Point(16.0, 6.2) });
        cloth.Segments.Add(new LineSegment { Point = new Point(9.8, 9.2) });
        geometry.Figures.Add(cloth);

        _flag = CreatePath(brush);
        _flag.Data = geometry;
        _flag.Stretch = Stretch.None;
        _flag.StrokeThickness = 0;
        ApplyIconScale(_flag);
        Root.Children.Add(_flag);
    }

    private void CreateMine(Brush brush)
    {
        var group = new GeometryGroup();
        group.Children.Add(new EllipseGeometry
        {
            Center = new Point(10, 10),
            RadiusX = 4.9,
            RadiusY = 4.9,
        });

        group.Children.Add(new RectangleGeometry { Rect = new Rect(2.1, 8.6, 15.8, 2.8) });
        group.Children.Add(new RectangleGeometry { Rect = new Rect(8.6, 2.1, 2.8, 15.8) });
        group.Children.Add(new RectangleGeometry
        {
            Rect = new Rect(2.6, 8.75, 14.8, 2.5),
            Transform = new RotateTransform { Angle = 45, CenterX = 10, CenterY = 10 },
        });
        group.Children.Add(new RectangleGeometry
        {
            Rect = new Rect(2.6, 8.75, 14.8, 2.5),
            Transform = new RotateTransform { Angle = -45, CenterX = 10, CenterY = 10 },
        });

        _mine = CreatePath(brush);
        _mine.Data = group;
        _mine.Stretch = Stretch.None;
        _mine.StrokeThickness = 0;
        ApplyIconScale(_mine);
        Root.Children.Add(_mine);
    }

    private void CreateCross(Brush brush)
    {
        var geometry = new PathGeometry();

        var first = new PathFigure { StartPoint = new Point(4.8, 4.8), IsFilled = false, IsClosed = false };
        first.Segments.Add(new LineSegment { Point = new Point(15.2, 15.2) });
        geometry.Figures.Add(first);

        var second = new PathFigure { StartPoint = new Point(15.2, 4.8), IsFilled = false, IsClosed = false };
        second.Segments.Add(new LineSegment { Point = new Point(4.8, 15.2) });
        geometry.Figures.Add(second);

        _cross = CreatePath(brush);
        _cross.Data = geometry;
        _cross.Fill = null;
        _cross.Stretch = Stretch.None;
        _cross.StrokeThickness = 2.2;
        ApplyIconScale(_cross);
        Root.Children.Add(_cross);
    }
}
