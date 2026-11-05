namespace WinMine.Core;

/// <summary>Zero-based board coordinate.</summary>
public readonly record struct Cell(int Row, int Column)
{
    public override string ToString() => $"({Row},{Column})";
}

public enum GameStatus
{
    /// <summary>Board created, no cell revealed yet. Mines are placed lazily on the first reveal.</summary>
    Ready,
    Playing,
    Won,
    Lost,
}

public enum MoveOutcome
{
    None,
    Revealed,
    Chorded,
    Flagged,
    Unflagged,
    Won,
    Lost,
}

/// <summary>Immutable view of a single cell, safe to hand to the UI layer.</summary>
public readonly record struct CellInfo(
    Cell Cell,
    bool IsRevealed,
    bool IsFlagged,
    bool IsMine,
    int AdjacentMines)
{
    public bool IsHidden => !IsRevealed;
    public bool IsRevealedNumber => IsRevealed && !IsMine && AdjacentMines > 0;
}

/// <summary>What a single player action changed.</summary>
public readonly record struct MoveResult(
    bool Accepted,
    MoveOutcome Outcome,
    IReadOnlyList<Cell> Revealed,
    GameStatus Status,
    Cell? ExplodedAt)
{
    public static MoveResult Ignored(GameStatus status) =>
        new(false, MoveOutcome.None, Array.Empty<Cell>(), status, null);
}
