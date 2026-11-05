using Microsoft.UI.Xaml.Controls;
using WinMine.Core;

namespace WinMine.Views;

public sealed partial class CustomGameDialog : ContentDialog
{
    private bool _initializing = true;

    public CustomGameDialog()
        : this(new BoardPreset(16, 16, 40))
    {
    }

    public CustomGameDialog(BoardPreset preset)
    {
        InitializeComponent();

        RowsBox.Minimum = Minefield.MinRows;
        RowsBox.Maximum = Minefield.MaxRows;
        ColumnsBox.Minimum = Minefield.MinColumns;
        ColumnsBox.Maximum = Minefield.MaxColumns;

        RowsBox.Value = Math.Clamp(preset.Rows, Minefield.MinRows, Minefield.MaxRows);
        ColumnsBox.Value = Math.Clamp(preset.Columns, Minefield.MinColumns, Minefield.MaxColumns);
        MinesBox.Value = Math.Clamp(preset.Mines, 1, (int)(RowsBox.Value * ColumnsBox.Value) - 1);

        _initializing = false;
        Validate();
    }

    /// <summary>The board the player confirmed, or null when the dialog was dismissed.</summary>
    public BoardPreset? Board { get; private set; }

    private void OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_initializing)
        {
            return;
        }

        Validate();
    }

    private void Validate()
    {
        int rows = ReadValue(RowsBox);
        int columns = ReadValue(ColumnsBox);
        int maxMines = Math.Max(1, (rows * columns) - 1);
        MinesBox.Maximum = maxMines;

        int mines = ReadValue(MinesBox);
        bool valid = Minefield.IsValid(rows, columns, mines, out string? error);

        ValidationBar.IsOpen = !valid;
        if (!valid && !string.IsNullOrEmpty(error))
        {
            ValidationBar.Message = error;
        }

        IsPrimaryButtonEnabled = valid;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        int rows = ReadValue(RowsBox);
        int columns = ReadValue(ColumnsBox);
        int mines = ReadValue(MinesBox);

        if (!Minefield.IsValid(rows, columns, mines, out string? error))
        {
            args.Cancel = true;
            ValidationBar.IsOpen = true;
            ValidationBar.Message = error ?? "参数无效";
            return;
        }

        Board = new BoardPreset(rows, columns, mines);
    }

    private static int ReadValue(NumberBox box) =>
        double.IsNaN(box.Value) ? (int)box.Minimum : (int)Math.Round(box.Value);
}
