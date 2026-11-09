using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinMine.Core;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Input;

namespace WinMine.Controls;

/// <summary>
/// Renders a <see cref="Minefield"/> as a Fluent-styled grid and translates pointer/keyboard input into moves.
/// All hit testing happens on the panel itself; tiles are not hit-testable.
/// </summary>
public sealed class BoardView : Panel
{
    private const double Gap = 3;
    private const double Pad = 8;
    private const int MaxAnimatedReveal = 60;

    private readonly Border _focusRing = new()
    {
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(6),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    private Minefield? _board;
    private CellVisual[] _cells = Array.Empty<CellVisual>();
    private BoardColors _colors = BoardPalette.For(false);
    private double _cellSize = 30;
    private int _hovered = -1;
    private int _pressed = -1;
    private Cell _cursor;
    private bool _cursorVisible;

    public BoardView()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = false;
        Background = new SolidColorBrush(Colors.Transparent);

        Children.Add(_focusRing);

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        PointerCaptureLost += OnPointerCaptureLost;
        KeyDown += OnKeyDown;
        GotFocus += (_, _) => UpdateFocusRing();
        LostFocus += (_, _) => UpdateFocusRing();
        Holding += OnHolding;
        ActualThemeChanged += (_, _) => ApplyTheme();
    }

    /// <summary>Raised after an accepted move so the host can update counters, the timer and the face.</summary>
    public event EventHandler<MoveResult>? MoveCompleted;

    /// <summary>Raised when a tile is visually depressed / released.</summary>
    public event EventHandler<bool>? PressStateChanged;

    public Minefield? Board => _board;

    public int Rows => _board?.Rows ?? 0;

    public int Columns => _board?.Columns ?? 0;

    public double CellSize => _cellSize;

    /// <summary>Number of tile visuals currently hosted (the focus ring is not counted).</summary>
    public int CellControlCount => _cells.Length;

    public double MeasureWidthFor(double cellSize) =>
        (Pad * 2) + (Columns * cellSize) + (Math.Max(0, Columns - 1) * Gap);

    public double MeasureHeightFor(double cellSize) =>
        (Pad * 2) + (Rows * cellSize) + (Math.Max(0, Rows - 1) * Gap);

    public void LoadBoard(Minefield board)
    {
        _board = board;
        _hovered = -1;
        _pressed = -1;
        _cursor = new Cell(board.Rows / 2, board.Columns / 2);
        _cursorVisible = false;
        RebuildCells();
        InvalidateMeasure();
        InvalidateArrange();
    }

    public void UpdateCellSize(double cellSize)
    {
        _cellSize = Math.Clamp(cellSize, 14, 64);
        foreach (var cell in _cells)
        {
            cell.SetSize(_cellSize);
        }

        InvalidateMeasure();
        InvalidateArrange();
    }

    public void ApplyTheme()
    {
        bool dark = ActualTheme == ElementTheme.Dark;
        _colors = BoardPalette.For(dark);
        Background = _colors.Surface;
        _focusRing.BorderBrush = _colors.Focus;
        RefreshAll();
    }

    public void RefreshAll()
    {
        foreach (var cell in _cells)
        {
            cell.StopMotion();
            ApplyCellVisual(cell);
        }
    }

    /// <summary>Composition state of one tile; --selftest uses it to prove motion was reset.</summary>
    internal (float Opacity, float ScaleX)? ProbeCellVisual(int index)
    {
        if (index < 0 || index >= _cells.Length)
        {
            return null;
        }

        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_cells[index].Surface);
        return (visual.Opacity, visual.Scale.X);
    }

    public void RevealAt(Cell cell) => Execute(cell, board => board.Reveal(cell));

    public void FlagAt(Cell cell) => Execute(cell, board => board.ToggleFlag(cell));

    public void ChordAt(Cell cell) => Execute(cell, board => board.Chord(cell));

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(_cellSize, _cellSize));
        }

        return new Size(MeasureWidthFor(_cellSize), MeasureHeightFor(_cellSize));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double pitch = _cellSize + Gap;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                var cell = _cells[(r * Columns) + c];
                cell.Root.Arrange(new Rect(Pad + (c * pitch), Pad + (r * pitch), _cellSize, _cellSize));
            }
        }

        if (_board is not null)
        {
            double x = Pad + (_cursor.Column * pitch);
            double y = Pad + (_cursor.Row * pitch);
            _focusRing.Arrange(new Rect(x - 1, y - 1, _cellSize + 2, _cellSize + 2));
        }

        return finalSize;
    }

    private void RebuildCells()
    {
        Children.Clear();
        if (_board is null)
        {
            _cells = Array.Empty<CellVisual>();
            Children.Add(_focusRing);
            return;
        }

        var cells = new CellVisual[_board.Rows * _board.Columns];
        for (int r = 0; r < _board.Rows; r++)
        {
            for (int c = 0; c < _board.Columns; c++)
            {
                var visual = new CellVisual(new Cell(r, c));
                visual.SetSize(_cellSize);
                cells[(r * _board.Columns) + c] = visual;
                Children.Add(visual.Root);
            }
        }

        _cells = cells;
        Children.Add(_focusRing);
        ApplyTheme();
    }

    private void ApplyCellVisual(CellVisual visual)
    {
        if (_board is null)
        {
            return;
        }

        int index = IndexOf(visual.Cell);
        var info = _board.GetCell(visual.Cell);
        bool gameOver = _board.Status is GameStatus.Won or GameStatus.Lost;
        bool hovered = !gameOver && index == _hovered;
        bool pressed = !gameOver && index == _pressed && index == _hovered;
        bool exploded = _board.ExplodedAt is { } mine && mine == visual.Cell;

        visual.Root.Opacity = 1;
        visual.Root.Scale = Vector3.One;
        visual.SetSize(_cellSize);

        bool truthMine = _board.IsMineAt(visual.Cell);
        bool mineExposed = truthMine && (info.IsRevealed || _board.Status == GameStatus.Lost);

        if (mineExposed)
        {
            visual.SetIcon(CellIcon.Mine, exploded ? _colors.ExplodedStroke : _colors.Mine);
            visual.Tile.Background = exploded ? _colors.Exploded : _colors.Revealed;
            visual.Tile.BorderBrush = exploded ? _colors.ExplodedStroke : _colors.RevealedGrid;
            visual.ClearText();
            return;
        }

        if (info.IsRevealed)
        {
            visual.ClearIcon();
            visual.Tile.Background = hovered ? _colors.RevealedHover : _colors.Revealed;
            visual.Tile.BorderBrush = _colors.RevealedGrid;

            if (info.AdjacentMines > 0)
            {
                visual.Text.Text = info.AdjacentMines.ToString();
                visual.Text.Foreground = _colors.Numbers[Math.Clamp(info.AdjacentMines - 1, 0, _colors.Numbers.Length - 1)];
                visual.Text.Visibility = Visibility.Visible;
            }
            else
            {
                visual.ClearText();
            }

            return;
        }

        visual.ClearText();
        visual.Tile.Background = pressed
            ? _colors.HiddenPressed
            : hovered ? _colors.HiddenHover : _colors.Hidden;
        visual.Tile.BorderBrush = _colors.HiddenStroke;

        if (info.IsFlagged)
        {
            bool wrong = gameOver && !truthMine;
            visual.SetIcon(wrong ? CellIcon.WrongFlag : CellIcon.Flag, wrong ? _colors.WrongFlag : _colors.Flag);
        }
        else
        {
            visual.ClearIcon();
        }

        if (pressed)
        {
            visual.Root.Scale = new Vector3(0.94f, 0.94f, 1f);
        }
    }

    private void Execute(Cell cell, Func<Minefield, MoveResult> move)
    {
        if (_board is null)
        {
            return;
        }

        var result = move(_board);
        if (!result.Accepted)
        {
            return;
        }

        if (result.Status is GameStatus.Won or GameStatus.Lost)
        {
            RefreshAll();

            if (result.Status is GameStatus.Lost && _board.ExplodedAt is { } mine)
            {
                _cells[IndexOf(mine)].PlayExplodeMotion();
            }
        }
        else
        {
            foreach (var opened in result.Revealed)
            {
                ApplyCellVisual(_cells[IndexOf(opened)]);
            }

            ApplyCellVisual(_cells[IndexOf(cell)]);

            if (result.Outcome is MoveOutcome.Flagged)
            {
                _cells[IndexOf(cell)].PlayFlagMotion();
            }

            AnimateReveal(cell, result.Revealed);
        }

        MoveCompleted?.Invoke(this, result);
    }

    /// <summary>
    /// Reveals ripple outwards: each opened tile starts its settle animation one wave later than
    /// its neighbour, so a big flood reads as a wave instead of a slab.
    /// </summary>
    private void AnimateReveal(Cell origin, IReadOnlyList<Cell> opened)
    {
        if (opened.Count is 0)
        {
            return;
        }

        Dictionary<int, int>? waves = opened.Count > 1 ? ComputeWaves(origin, opened) : null;

        foreach (var openedCell in opened)
        {
            var visual = _cells[IndexOf(openedCell)];
            int wave = waves is not null && waves.TryGetValue(IndexOf(openedCell), out int found) ? found : 0;
            visual.PlayRevealMotion(CellMotion.RevealDelay(wave));
        }
    }

    private Dictionary<int, int> ComputeWaves(Cell origin, IReadOnlyList<Cell> opened)
    {
        var waves = new Dictionary<int, int>(opened.Count);
        if (_board is null)
        {
            return waves;
        }

        var open = new HashSet<int>(opened.Count);
        foreach (var cell in opened)
        {
            open.Add(IndexOf(cell));
        }

        var queue = new Queue<int>();
        int originIndex = IndexOf(origin);
        if (open.Contains(originIndex))
        {
            waves[originIndex] = 0;
            queue.Enqueue(originIndex);
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            int next = waves[current] + 1;
            var cell = _cells[current].Cell;

            foreach (var neighbour in _board.Neighbours(cell))
            {
                int index = IndexOf(neighbour);
                if (open.Contains(index) && !waves.ContainsKey(index))
                {
                    waves[index] = next;
                    queue.Enqueue(index);
                }
            }
        }

        return waves;
    }

    private Cell? HitTestCell(Point position)
    {
        if (_board is null)
        {
            return null;
        }

        double pitch = _cellSize + Gap;
        double x = position.X - Pad;
        double y = position.Y - Pad;
        if (x < 0 || y < 0)
        {
            return null;
        }

        int column = (int)(x / pitch);
        int row = (int)(y / pitch);
        if (row < 0 || row >= Rows || column < 0 || column >= Columns)
        {
            return null;
        }

        return new Cell(row, column);
    }

    private int IndexOf(Cell cell) => (cell.Row * Columns) + cell.Column;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_board is null)
        {
            return;
        }

        Focus(FocusState.Pointer);
        if (_board.Status is GameStatus.Won or GameStatus.Lost)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var cell = HitTestCell(point.Position);
        if (cell is null)
        {
            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            FlagAt(cell.Value);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsMiddleButtonPressed)
        {
            ChordAt(cell.Value);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _hovered = IndexOf(cell.Value);
        _pressed = _hovered;
        ApplyCellVisual(_cells[_pressed]);
        PressStateChanged?.Invoke(this, true);
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_board is null || _pressed < 0)
        {
            return;
        }

        int pressed = _pressed;
        _pressed = -1;
        ReleasePointerCapture(e.Pointer);

        var point = e.GetCurrentPoint(this);
        var cell = HitTestCell(point.Position);
        var visual = _cells[pressed];

        if (cell is not null && IndexOf(cell.Value) == pressed && _board.Status is GameStatus.Ready or GameStatus.Playing)
        {
            _hovered = pressed;
            ApplyCellVisual(visual);
            var info = _board.GetCell(cell.Value);
            if (info.IsRevealed && info.AdjacentMines > 0)
            {
                ChordAt(cell.Value);
            }
            else
            {
                RevealAt(cell.Value);
            }
        }

        ApplyCellVisual(visual);
        PressStateChanged?.Invoke(this, false);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_board is null)
        {
            return;
        }

        var cell = HitTestCell(e.GetCurrentPoint(this).Position);
        int index = cell is null ? -1 : IndexOf(cell.Value);
        if (index == _hovered)
        {
            return;
        }

        int previous = _hovered;
        _hovered = index;
        if (previous >= 0)
        {
            ApplyCellVisual(_cells[previous]);
        }

        if (index >= 0)
        {
            ApplyCellVisual(_cells[index]);
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_hovered >= 0 && _board is not null)
        {
            int previous = _hovered;
            _hovered = -1;
            ApplyCellVisual(_cells[previous]);
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_board is null || _pressed < 0)
        {
            return;
        }

        int pressed = _pressed;
        _pressed = -1;
        ApplyCellVisual(_cells[pressed]);
    }

    private void OnHolding(object sender, HoldingRoutedEventArgs e)
    {
        if (_board is null || !e.HoldingState.Equals(HoldingState.Started))
        {
            return;
        }

        var cell = HitTestCell(e.GetPosition(this));
        if (cell is not null)
        {
            FlagAt(cell.Value);
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_board is null)
        {
            return;
        }

        bool moved = false;
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.GamepadDPadLeft:
                _cursor = new Cell(_cursor.Row, Math.Max(0, _cursor.Column - 1));
                moved = true;
                break;
            case VirtualKey.Right:
            case VirtualKey.GamepadDPadRight:
                _cursor = new Cell(_cursor.Row, Math.Min(Columns - 1, _cursor.Column + 1));
                moved = true;
                break;
            case VirtualKey.Up:
            case VirtualKey.GamepadDPadUp:
                _cursor = new Cell(Math.Max(0, _cursor.Row - 1), _cursor.Column);
                moved = true;
                break;
            case VirtualKey.Down:
            case VirtualKey.GamepadDPadDown:
                _cursor = new Cell(Math.Min(Rows - 1, _cursor.Row + 1), _cursor.Column);
                moved = true;
                break;
            case VirtualKey.Space:
            case VirtualKey.Enter:
            {
                var info = _board.GetCell(_cursor);
                if (info.IsRevealed && info.AdjacentMines > 0)
                {
                    ChordAt(_cursor);
                }
                else
                {
                    RevealAt(_cursor);
                }

                e.Handled = true;
                break;
            }

            case VirtualKey.F:
                FlagAt(_cursor);
                e.Handled = true;
                break;
        }

        if (moved)
        {
            _cursorVisible = true;
            UpdateFocusRing();
            InvalidateArrange();
            e.Handled = true;
        }
    }

    private void UpdateFocusRing()
    {
        bool focused = FocusState != FocusState.Unfocused;
        _focusRing.Visibility = focused && _cursorVisible ? Visibility.Visible : Visibility.Collapsed;
        InvalidateArrange();
    }
}
