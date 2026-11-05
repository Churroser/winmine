using Microsoft.UI.Xaml.Controls;
using WinMine.Core;
using WinMine.Services;

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

        ApplyLocalization();

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

    /// <summary>Pulls every user-visible string from the localized string table.</summary>
    private void ApplyLocalization()
    {
        Title = Loc.T("custom.title");
        PrimaryButtonText = Loc.T("custom.primary");
        CloseButtonText = Loc.T("custom.cancel");
        ColumnsLabel.Text = Loc.T("custom.columns");
        RowsLabel.Text = Loc.T("custom.rows");
        MinesLabel.Text = Loc.T("custom.mines");
        HintText.Text = Loc.T("custom.hint");
        ValidationBar.Message = Loc.T("custom.invalid");
    }

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
        bool valid = Minefield.TryValidate(rows, columns, mines, out BoardValidationError error);

        ValidationBar.IsOpen = !valid;
        if (!valid)
        {
            ValidationBar.Message = Loc.DescribeBoardError(error, rows, columns);
        }

        IsPrimaryButtonEnabled = valid;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        int rows = ReadValue(RowsBox);
        int columns = ReadValue(ColumnsBox);
        int mines = ReadValue(MinesBox);

        if (!Minefield.TryValidate(rows, columns, mines, out BoardValidationError error))
        {
            args.Cancel = true;
            ValidationBar.IsOpen = true;
            ValidationBar.Message = Loc.DescribeBoardError(error, rows, columns);
            return;
        }

        Board = new BoardPreset(rows, columns, mines);
    }

    private static int ReadValue(NumberBox box) =>
        double.IsNaN(box.Value) ? (int)box.Minimum : (int)Math.Round(box.Value);
}

