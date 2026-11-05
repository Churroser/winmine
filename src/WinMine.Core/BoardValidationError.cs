namespace WinMine.Core;

/// <summary>Why a board definition was rejected, so the UI can localize the explanation itself.</summary>
public enum BoardValidationError
{
    None = 0,
    RowsOutOfRange,
    ColumnsOutOfRange,
    MinesOutOfRange,
}
