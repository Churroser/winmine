namespace WinMine.Core;

/// <summary>
/// Classic minesweeper rules.
/// <list type="bullet">
/// <item>Mines are placed lazily on the first reveal, never on/next to that cell (when the board allows it).</item>
/// <item>Flood fill opens connected zero regions but never opens flagged cells.</item>
/// <item>Chording opens the hidden neighbours of a number when the surrounding flag count matches.</item>
/// <item>Winning auto-flags every remaining mine; losing reveals every mine.</item>
/// </list>
/// </summary>
public sealed class Minefield
{
    public const int MinRows = 3;
    public const int MaxRows = 30;
    public const int MinColumns = 3;
    public const int MaxColumns = 40;

    private readonly bool[] _mines;
    private readonly bool[] _revealed;
    private readonly bool[] _flagged;
    private readonly int[] _adjacent;
    private readonly Random _random;

    public Minefield(int rows, int columns, int mineCount, int? seed = null)
    {
        Validate(rows, columns, mineCount);
        Rows = rows;
        Columns = columns;
        MineCount = mineCount;
        _random = seed is null ? new Random() : new Random(seed.Value);

        int length = rows * columns;
        _mines = new bool[length];
        _revealed = new bool[length];
        _flagged = new bool[length];
        _adjacent = new int[length];
    }

    public int Rows { get; }

    public int Columns { get; }

    public int MineCount { get; }

    public int CellCount => Rows * Columns;

    public int SafeCellCount => CellCount - MineCount;

    public GameStatus Status { get; private set; } = GameStatus.Ready;

    public bool MinesPlaced { get; private set; }

    public int FlagCount { get; private set; }

    public int RemainingMines => MineCount - FlagCount;

    /// <summary>Number of safe cells that are currently open.</summary>
    public int RevealedCount { get; private set; }

    public Cell? ExplodedAt { get; private set; }

    public static void Validate(int rows, int columns, int mineCount)
    {
        if (rows is < MinRows or > MaxRows)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, $"行数必须在 {MinRows} 到 {MaxRows} 之间。");
        }

        if (columns is < MinColumns or > MaxColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, $"列数必须在 {MinColumns} 到 {MaxColumns} 之间。");
        }

        if (mineCount < 1 || mineCount > rows * columns - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mineCount), mineCount, "雷数必须介于 1 与格子总数减一之间。");
        }
    }

    /// <summary>
    /// Checks a board definition and reports <em>why</em> it was rejected. The UI localizes the
    /// message from the code; <see cref="IsValid(int, int, int, out string?)"/> keeps the original
    /// Chinese text for engine-side use and existing callers.
    /// </summary>
    public static bool TryValidate(int rows, int columns, int mineCount, out BoardValidationError error)
    {
        if (rows is < MinRows or > MaxRows)
        {
            error = BoardValidationError.RowsOutOfRange;
            return false;
        }

        if (columns is < MinColumns or > MaxColumns)
        {
            error = BoardValidationError.ColumnsOutOfRange;
            return false;
        }

        if (mineCount < 1 || mineCount > rows * columns - 1)
        {
            error = BoardValidationError.MinesOutOfRange;
            return false;
        }

        error = BoardValidationError.None;
        return true;
    }

    public static bool IsValid(int rows, int columns, int mineCount, out string? error)
    {
        bool valid = TryValidate(rows, columns, mineCount, out BoardValidationError code);
        error = code switch
        {
            BoardValidationError.RowsOutOfRange => $"行数需要在 {MinRows}–{MaxRows} 之间",
            BoardValidationError.ColumnsOutOfRange => $"列数需要在 {MinColumns}–{MaxColumns} 之间",
            BoardValidationError.MinesOutOfRange => $"雷数需要在 1–{rows * columns - 1} 之间",
            _ => null,
        };

        return valid;
    }

    public bool InBounds(Cell cell) =>
        cell.Row >= 0 && cell.Row < Rows && cell.Column >= 0 && cell.Column < Columns;

    public CellInfo GetCell(Cell cell)
    {
        if (!InBounds(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell));
        }

        int index = IndexOf(cell);
        bool mineVisible = _mines[index] && (Status == GameStatus.Lost || _revealed[index]);
        return new CellInfo(cell, _revealed[index], _flagged[index], mineVisible, _adjacent[index]);
    }

    /// <summary>Ground truth, for tests and for the loss overlay. Do not expose to gameplay code.</summary>
    public bool IsMineAt(Cell cell)
    {
        if (!InBounds(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell));
        }

        return _mines[IndexOf(cell)];
    }

    public IReadOnlyList<Cell> MinePositions()
    {
        var list = new List<Cell>(MineCount);
        for (int i = 0; i < _mines.Length; i++)
        {
            if (_mines[i])
            {
                list.Add(CellAt(i));
            }
        }

        return list;
    }

    /// <summary>Every cell of the board, row-major.</summary>
    public IEnumerable<Cell> AllCells()
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                yield return new Cell(r, c);
            }
        }
    }

    /// <summary>The cells a player can currently see (used by diagnostics and hints).</summary>
    public IEnumerable<Cell> RevealedCells()
    {
        foreach (var cell in AllCells())
        {
            if (_revealed[IndexOf(cell)])
            {
                yield return cell;
            }
        }
    }

    public IReadOnlyList<Cell> Neighbours(Cell cell)
    {
        if (!InBounds(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell));
        }

        var list = new List<Cell>(8);
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                var candidate = new Cell(cell.Row + dr, cell.Column + dc);
                if (InBounds(candidate))
                {
                    list.Add(candidate);
                }
            }
        }

        return list;
    }

    public MoveResult Reveal(Cell cell)
    {
        if (!InBounds(cell) || Status is GameStatus.Won or GameStatus.Lost)
        {
            return MoveResult.Ignored(Status);
        }

        int index = IndexOf(cell);
        if (_revealed[index] || _flagged[index])
        {
            return MoveResult.Ignored(Status);
        }

        EnsureMinesPlaced(index);
        Status = GameStatus.Playing;

        if (_mines[index])
        {
            return Explode(index);
        }

        var opened = new List<Cell>(16);
        FloodFrom(index, opened);
        return FinishMove(MoveOutcome.Revealed, opened);
    }

    public MoveResult ToggleFlag(Cell cell)
    {
        if (!InBounds(cell) || Status is GameStatus.Won or GameStatus.Lost)
        {
            return MoveResult.Ignored(Status);
        }

        int index = IndexOf(cell);
        if (_revealed[index])
        {
            return MoveResult.Ignored(Status);
        }

        _flagged[index] = !_flagged[index];
        FlagCount += _flagged[index] ? 1 : -1;
        var outcome = _flagged[index] ? MoveOutcome.Flagged : MoveOutcome.Unflagged;
        return new MoveResult(true, outcome, Array.Empty<Cell>(), Status, null);
    }

    public MoveResult Chord(Cell cell)
    {
        if (!InBounds(cell) || Status is GameStatus.Won or GameStatus.Lost)
        {
            return MoveResult.Ignored(Status);
        }

        int index = IndexOf(cell);
        if (!_revealed[index] || _adjacent[index] == 0)
        {
            return MoveResult.Ignored(Status);
        }

        int flags = 0;
        foreach (var neighbour in Neighbours(cell))
        {
            if (_flagged[IndexOf(neighbour)])
            {
                flags++;
            }
        }

        if (flags != _adjacent[index])
        {
            return MoveResult.Ignored(Status);
        }

        var opened = new List<Cell>(8);
        foreach (var neighbour in Neighbours(cell))
        {
            int neighbourIndex = IndexOf(neighbour);
            if (_revealed[neighbourIndex] || _flagged[neighbourIndex])
            {
                continue;
            }

            if (_mines[neighbourIndex])
            {
                return Explode(neighbourIndex, opened);
            }

            FloodFrom(neighbourIndex, opened);
        }

        if (opened.Count == 0)
        {
            return MoveResult.Ignored(Status);
        }

        return FinishMove(MoveOutcome.Chorded, opened);
    }

    private MoveResult FinishMove(MoveOutcome outcome, List<Cell> opened)
    {
        if (RevealedCount >= SafeCellCount)
        {
            return Win(opened);
        }

        return new MoveResult(true, outcome, opened, Status, null);
    }

    private MoveResult Win(List<Cell> opened)
    {
        Status = GameStatus.Won;
        for (int i = 0; i < _mines.Length; i++)
        {
            if (_mines[i] && !_flagged[i])
            {
                _flagged[i] = true;
                FlagCount++;
            }
        }

        return new MoveResult(true, MoveOutcome.Won, opened, Status, null);
    }

    private MoveResult Explode(int mineIndex) => Explode(mineIndex, new List<Cell>());

    private MoveResult Explode(int mineIndex, List<Cell> opened)
    {
        Status = GameStatus.Lost;
        ExplodedAt = CellAt(mineIndex);

        for (int i = 0; i < _mines.Length; i++)
        {
            if (_mines[i] && !_revealed[i])
            {
                _revealed[i] = true;
                opened.Add(CellAt(i));
            }
        }

        return new MoveResult(true, MoveOutcome.Lost, opened, Status, ExplodedAt);
    }

    private void FloodFrom(int startIndex, List<Cell> opened)
    {
        var stack = new Stack<int>();
        stack.Push(startIndex);

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            if (_revealed[index] || _flagged[index])
            {
                continue;
            }

            _revealed[index] = true;
            if (!_mines[index])
            {
                RevealedCount++;
            }

            opened.Add(CellAt(index));

            if (_adjacent[index] != 0 || _mines[index])
            {
                continue;
            }

            foreach (var neighbour in Neighbours(CellAt(index)))
            {
                int neighbourIndex = IndexOf(neighbour);
                if (!_revealed[neighbourIndex] && !_flagged[neighbourIndex])
                {
                    stack.Push(neighbourIndex);
                }
            }
        }
    }

    private void EnsureMinesPlaced(int safeIndex)
    {
        if (MinesPlaced)
        {
            return;
        }

        var forbidden = new bool[CellCount];
        forbidden[safeIndex] = true;
        int safeRow = safeIndex / Columns;
        int safeColumn = safeIndex % Columns;

        bool roomForSafeZone = CellCount - 9 >= MineCount;
        if (roomForSafeZone)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = safeRow + dr;
                    int c = safeColumn + dc;
                    if (r >= 0 && r < Rows && c >= 0 && c < Columns)
                    {
                        forbidden[r * Columns + c] = true;
                    }
                }
            }
        }

        var candidates = new List<int>(CellCount);
        for (int i = 0; i < CellCount; i++)
        {
            if (!forbidden[i])
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count < MineCount)
        {
            // Extremely dense custom boards: only guarantee the very first click is safe.
            candidates.Clear();
            for (int i = 0; i < CellCount; i++)
            {
                if (i != safeIndex)
                {
                    candidates.Add(i);
                }
            }
        }

        for (int k = 0; k < MineCount; k++)
        {
            int j = k + _random.Next(candidates.Count - k);
            (candidates[k], candidates[j]) = (candidates[j], candidates[k]);
            _mines[candidates[k]] = true;
        }

        for (int i = 0; i < CellCount; i++)
        {
            int adjacent = 0;
            foreach (var neighbour in Neighbours(CellAt(i)))
            {
                if (_mines[IndexOf(neighbour)])
                {
                    adjacent++;
                }
            }

            _adjacent[i] = adjacent;
        }

        MinesPlaced = true;
    }

    private int IndexOf(Cell cell) => (cell.Row * Columns) + cell.Column;

    private Cell CellAt(int index) => new(index / Columns, index % Columns);
}
