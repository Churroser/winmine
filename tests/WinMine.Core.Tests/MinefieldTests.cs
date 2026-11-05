using WinMine.Core;
using Xunit;

namespace WinMine.Core.Tests;

public class MinefieldTests
{
    [Theory]
    [InlineData(2, 9, 10)]
    [InlineData(31, 9, 10)]
    [InlineData(9, 2, 10)]
    [InlineData(9, 41, 10)]
    [InlineData(9, 9, 0)]
    [InlineData(9, 9, 81)]
    public void Layout_rejects_impossible_boards(int rows, int columns, int mines)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Minefield(rows, columns, mines));
    }

    [Fact]
    public void First_reveal_is_never_a_mine_and_always_opens_a_zero_zone()
    {
        for (int seed = 0; seed < 120; seed++)
        {
            var field = new Minefield(9, 9, 10, seed);
            var result = field.Reveal(new Cell(4, 4));

            Assert.True(result.Accepted);
            Assert.Null(result.ExplodedAt);
            Assert.Equal(0, field.GetCell(new Cell(4, 4)).AdjacentMines);
            Assert.True(field.RevealedCount >= 9, $"seed {seed} opened only {field.RevealedCount} cells");

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    Assert.True(field.GetCell(new Cell(4 + dr, 4 + dc)).IsRevealed, $"seed {seed} left ({4 + dr},{4 + dc}) closed");
                }
            }
        }
    }

    [Fact]
    public void Exactly_the_requested_number_of_mines_is_placed()
    {
        var field = new Minefield(16, 30, 99, 7);
        field.Reveal(new Cell(0, 0));

        Assert.True(field.MinesPlaced);
        Assert.Equal(99, field.MinePositions().Count);
    }

    [Fact]
    public void Adjacent_counts_match_brute_force()
    {
        var field = new Minefield(16, 30, 99, 4242);
        field.Reveal(new Cell(8, 15));

        for (int r = 0; r < field.Rows; r++)
        {
            for (int c = 0; c < field.Columns; c++)
            {
                int expected = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                        {
                            continue;
                        }

                        int nr = r + dr;
                        int nc = c + dc;
                        if (nr >= 0 && nr < field.Rows && nc >= 0 && nc < field.Columns && field.IsMineAt(new Cell(nr, nc)))
                        {
                            expected++;
                        }
                    }
                }

                Assert.Equal(expected, field.GetCell(new Cell(r, c)).AdjacentMines);
            }
        }
    }

    [Fact]
    public void Flagging_updates_the_remaining_mine_counter_and_can_go_negative()
    {
        var field = new Minefield(9, 9, 10, 1);
        Assert.Equal(10, field.RemainingMines);

        for (int i = 0; i < 12; i++)
        {
            var result = field.ToggleFlag(new Cell(i / 9, i % 9));
            Assert.True(result.Accepted);
        }

        Assert.Equal(12, field.FlagCount);
        Assert.Equal(-2, field.RemainingMines);

        field.ToggleFlag(new Cell(0, 0));
        Assert.Equal(11, field.FlagCount);
    }

    [Fact]
    public void Flagged_cells_cannot_be_revealed_and_open_cells_cannot_be_flagged()
    {
        var field = new Minefield(9, 9, 10, 3);
        var cell = new Cell(2, 2);

        field.ToggleFlag(cell);
        Assert.False(field.Reveal(cell).Accepted);
        Assert.False(field.GetCell(cell).IsRevealed);

        field.ToggleFlag(cell);
        field.Reveal(new Cell(8, 8));
        var opened = new Cell(8, 8);
        Assert.True(field.GetCell(opened).IsRevealed);
        Assert.False(field.ToggleFlag(opened).Accepted);
    }

    [Fact]
    public void Flood_fill_opens_connected_zero_regions_completely()
    {
        var field = new Minefield(16, 16, 40, 99);
        field.Reveal(new Cell(8, 8));

        for (int r = 0; r < field.Rows; r++)
        {
            for (int c = 0; c < field.Columns; c++)
            {
                var cell = new Cell(r, c);
                var info = field.GetCell(cell);
                if (!info.IsRevealed || info.IsMine || info.AdjacentMines != 0)
                {
                    continue;
                }

                foreach (var neighbour in field.Neighbours(cell))
                {
                    Assert.True(field.GetCell(neighbour).IsRevealed, $"zero at {cell} left {neighbour} closed");
                }
            }
        }
    }

    [Fact]
    public void Flags_block_flood_fill()
    {
        var field = new Minefield(9, 9, 10, 5);
        var target = new Cell(0, 0);
        field.ToggleFlag(target);
        field.Reveal(new Cell(8, 8));

        if (field.Status != GameStatus.Lost)
        {
            // The flagged corner must stay closed even if it sits inside an opened zero region.
            Assert.False(field.GetCell(target).IsRevealed);
        }
    }

    [Fact]
    public void Chording_opens_hidden_neighbours_when_flags_match()
    {
        bool tested = false;
        for (int seed = 0; seed < 300 && !tested; seed++)
        {
            var field = new Minefield(9, 9, 10, seed);
            field.Reveal(new Cell(4, 4));
            if (field.Status != GameStatus.Playing)
            {
                continue;
            }

            for (int r = 0; r < 9 && !tested; r++)
            {
                for (int c = 0; c < 9 && !tested; c++)
                {
                    var cell = new Cell(r, c);
                    var info = field.GetCell(cell);
                    if (!info.IsRevealed || info.AdjacentMines == 0 || info.IsMine)
                    {
                        continue;
                    }

                    var neighbours = field.Neighbours(cell);
                    var mineNeighbours = neighbours.Where(field.IsMineAt).ToList();
                    if (mineNeighbours.Count != info.AdjacentMines)
                    {
                        continue;
                    }

                    bool hasHiddenSafe = neighbours.Any(n => !field.GetCell(n).IsRevealed && !field.IsMineAt(n));
                    if (!hasHiddenSafe)
                    {
                        continue;
                    }

                    foreach (var mine in mineNeighbours)
                    {
                        field.ToggleFlag(mine);
                    }

                    var before = field.RevealedCount;
                    var result = field.Chord(cell);

                    Assert.True(result.Accepted);
                    Assert.Equal(GameStatus.Playing, result.Status);
                    Assert.True(field.RevealedCount > before);
                    foreach (var neighbour in neighbours)
                    {
                        if (!field.IsMineAt(neighbour))
                        {
                            Assert.True(field.GetCell(neighbour).IsRevealed);
                        }
                    }

                    tested = true;
                }
            }
        }

        Assert.True(tested, "no chording configuration found");
    }

    [Fact]
    public void Chording_does_nothing_when_the_flag_count_mismatches()
    {
        var field = new Minefield(9, 9, 10, 11);
        field.Reveal(new Cell(4, 4));

        var number = FindRevealedNumber(field) ?? throw new InvalidOperationException("no number cell");
        var before = field.RevealedCount;
        var result = field.Chord(number);
        Assert.False(result.Accepted);
        Assert.Equal(before, field.RevealedCount);
    }

    [Fact]
    public void Chording_onto_an_unflagged_mine_loses_the_game()
    {
        bool tested = false;
        for (int seed = 0; seed < 500 && !tested; seed++)
        {
            var field = new Minefield(9, 9, 10, seed);
            field.Reveal(new Cell(1, 1));
            if (field.Status != GameStatus.Playing)
            {
                continue;
            }

            foreach (var number in AllCells(field).Where(c => field.GetCell(c) is { IsRevealed: true, IsMine: false, AdjacentMines: > 0 }))
            {
                var neighbours = field.Neighbours(number);
                var mines = neighbours.Where(field.IsMineAt).ToList();
                var safes = neighbours.Where(n => !field.IsMineAt(n) && !field.GetCell(n).IsRevealed && !field.GetCell(n).IsFlagged).ToList();
                if (mines.Count == 0 || safes.Count == 0)
                {
                    continue;
                }

                // Deliberately flag a safe neighbour while leaving a real mine unflagged.
                int flags = field.GetCell(number).AdjacentMines;
                var toFlag = new List<Cell> { safes[0] };
                toFlag.AddRange(mines.Take(flags - 1));
                if (toFlag.Count != flags)
                {
                    continue;
                }

                foreach (var flag in toFlag)
                {
                    field.ToggleFlag(flag);
                }

                var result = field.Chord(number);
                Assert.True(result.Accepted);
                Assert.Equal(GameStatus.Lost, result.Status);
                Assert.NotNull(field.ExplodedAt);
                Assert.True(field.GetCell(field.ExplodedAt!.Value).IsMine);
                tested = true;
                break;
            }
        }

        Assert.True(tested, "no chord-explosion configuration found");
    }

    [Fact]
    public void Revealing_every_safe_cell_wins_and_auto_flags_all_mines()
    {
        var field = new Minefield(9, 9, 10, 2024);
        field.Reveal(new Cell(4, 4));

        foreach (var cell in AllCells(field))
        {
            if (!field.IsMineAt(cell) && !field.GetCell(cell).IsRevealed)
            {
                field.Reveal(cell);
            }
        }

        Assert.Equal(GameStatus.Won, field.Status);
        Assert.Equal(10, field.FlagCount);
        Assert.All(field.MinePositions(), mine => Assert.True(field.GetCell(mine).IsFlagged));
        Assert.Equal(field.SafeCellCount, field.RevealedCount);
    }

    [Fact]
    public void Hitting_a_mine_loses_and_exposes_every_mine()
    {
        var field = new Minefield(9, 9, 10, 77);
        field.Reveal(new Cell(0, 0));
        var mine = field.MinePositions()[0];

        var result = field.Reveal(mine);

        Assert.True(result.Accepted);
        Assert.Equal(GameStatus.Lost, result.Status);
        Assert.Equal(mine, field.ExplodedAt);
        Assert.All(field.MinePositions(), m => Assert.True(field.GetCell(m).IsRevealed));
    }

    [Fact]
    public void Moves_after_the_game_ends_are_ignored()
    {
        var field = new Minefield(9, 9, 10, 123);
        field.Reveal(new Cell(4, 4));
        field.Reveal(field.MinePositions()[0]);
        Assert.Equal(GameStatus.Lost, field.Status);

        var hidden = AllCells(field).First(c => !field.GetCell(c).IsRevealed && !field.GetCell(c).IsFlagged);
        Assert.False(field.Reveal(hidden).Accepted);
        Assert.False(field.ToggleFlag(hidden).Accepted);
        Assert.False(field.Chord(hidden).Accepted);
    }

    [Fact]
    public void A_dense_custom_board_only_guarantees_the_first_cell_is_safe()
    {
        var field = new Minefield(3, 3, 8, 5);
        var result = field.Reveal(new Cell(1, 1));

        Assert.True(result.Accepted);
        Assert.Equal(GameStatus.Won, result.Status);
        Assert.False(field.IsMineAt(new Cell(1, 1)));
        Assert.Equal(8, field.MinePositions().Count);
        Assert.Equal(1, field.RevealedCount);
    }

    [Fact]
    public void The_same_seed_produces_the_same_board()
    {
        var a = new Minefield(16, 16, 40, 555);
        var b = new Minefield(16, 16, 40, 555);
        a.Reveal(new Cell(3, 3));
        b.Reveal(new Cell(3, 3));

        Assert.Equal(a.MinePositions(), b.MinePositions());
    }

    [Fact]
    public void Corner_cells_have_three_neighbours()
    {
        var field = new Minefield(9, 9, 10, 1);
        Assert.Equal(3, field.Neighbours(new Cell(0, 0)).Count);
        Assert.Equal(5, field.Neighbours(new Cell(0, 4)).Count);
        Assert.Equal(8, field.Neighbours(new Cell(4, 4)).Count);
    }

    [Fact]
    public void Expert_preset_uses_its_own_geometry()
    {
        var field = new Minefield(BoardPreset.Expert.Rows, BoardPreset.Expert.Columns, BoardPreset.Expert.Mines, 8);
        field.Reveal(new Cell(8, 15));

        Assert.Equal(16, field.Rows);
        Assert.Equal(30, field.Columns);
        Assert.Equal(99, field.MinePositions().Count);
    }

    private static Cell? FindRevealedNumber(Minefield field)
    {
        foreach (var cell in AllCells(field))
        {
            var info = field.GetCell(cell);
            if (info.IsRevealed && !info.IsMine && info.AdjacentMines > 0)
            {
                return cell;
            }
        }

        return null;
    }

    private static IEnumerable<Cell> AllCells(Minefield field)
    {
        for (int r = 0; r < field.Rows; r++)
        {
            for (int c = 0; c < field.Columns; c++)
            {
                yield return new Cell(r, c);
            }
        }
    }
}
