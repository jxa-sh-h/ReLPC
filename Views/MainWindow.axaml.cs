using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReLPC.Models;
using ReLPC.Services;
using ReLPC.ViewModels;

namespace ReLPC;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindowViewModel MenuViewModel { get; } = new();
    public ObservableCollection<DataPoint> DataPoints { get; }
    public ObservableCollection<Prediction> Predictions { get; } = [];
    public ObservableCollection<DatasetRecord> UserDatasets { get; } = [];

    private string _equation = "No calculation yet";
    private string _coefficient = "-";
    private string _intermediateComputations = "-";
    private string _currentDatasetName = "";
    private int _currentDatasetId;
    private bool _suppressDatasetListSelection;
    private bool _loadingDataset;
    private bool _undoRedoMuted;

    private readonly List<WorkspaceSnapshot> _undoStack = [];
    private readonly List<WorkspaceSnapshot> _redoStack = [];
    private WorkspaceSnapshot? _dataEditBaseline;
    private WorkspaceSnapshot? _titleEditBaseline;

    private const int MaxUndoSteps = 50;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public string CurrentDatasetName
    {
        get => _currentDatasetName;
        set => SetProperty(ref _currentDatasetName, value ?? "");
    }

    public string Equation
    {
        get => _equation;
        private set => SetProperty(ref _equation, value);
    }

    public string Coefficient
    {
        get => _coefficient;
        private set => SetProperty(ref _coefficient, value);
    }

    public string IntermediateComputations
    {
        get => _intermediateComputations;
        private set => SetProperty(ref _intermediateComputations, value);
    }

    public MainWindow()
    {
        Console.WriteLine("MainWindow constructor.");
        InitializeComponent();
        UpdateDegreeComboBoxVisibility();

        DataPoints = new ObservableCollection<DataPoint>(
            Enumerable.Range(0, 3).Select(_ => new DataPoint()));

        DataContext = this;

        RefreshUserDatasetsList();

        KeyDown += OnWindowKeyDown;

        Console.WriteLine("MainWindow initialized.");
    }

    public MainWindow(DatasetRecord dataset) : this()
    {
        LoadDataset(dataset);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private async void OnLogoutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var confirmLogoutWindow = new ConfirmLogoutWindow();
        var shouldLogout = await confirmLogoutWindow.ShowDialog<bool>(this);
        if (!shouldLogout)
        {
            return;
        }

        var loginWindow = new LoginWindow();
        DesktopSession.ShowAsMainWindow(loginWindow);
        Close();
    }

    private void OnFileExplorerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var shouldShowDataSetPanel = !DataSetPanel.IsVisible;
        DataSetPanel.IsVisible = shouldShowDataSetPanel;
        MainContentGrid.ColumnDefinitions[1].Width = shouldShowDataSetPanel
            ? new GridLength(166)
            : new GridLength(0);
    }

    private void OnNewDatasetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CreateBlankDataset();
    }

    private async void OnLoadDatasetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dataset = await PickDatasetAsync();
        TryLoadDatasetFromDb(dataset);
    }

    private void OnSaveDatasetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveCurrentDataset();
    }

    private void OnUserDatasetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressDatasetListSelection)
        {
            return;
        }

        // Use AddedItems (not SelectedItem). Reading SelectedItem during collection churn can mis-fire and
        // load inside Avalonia's selection logic — deferring avoids crashes when switching datasets.
        if (e.AddedItems.Count != 1 || e.AddedItems[0] is not DatasetRecord selected)
        {
            return;
        }

        if (selected.Id == _currentDatasetId)
        {
            return;
        }

        var pickedId = selected.Id;
        Dispatcher.UIThread.Post(
            () => TryLoadDatasetById(pickedId),
            DispatcherPriority.Background);
    }

    private void OnDatasetTitleGotFocus(object? sender, RoutedEventArgs e)
    {
        if (_undoRedoMuted)
        {
            return;
        }

        _titleEditBaseline ??= CaptureSnapshot();
    }

    private void OnDatasetTitleLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitBaseline(ref _titleEditBaseline);

        if (_loadingDataset || _suppressDatasetListSelection)
        {
            return;
        }

        PersistDatasetNameOnly();
    }

    private void OnDataCellGotFocus(object? sender, RoutedEventArgs e)
    {
        if (_undoRedoMuted)
        {
            return;
        }

        _dataEditBaseline ??= CaptureSnapshot();
    }

    private void OnDataCellLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitBaseline(ref _dataEditBaseline);
    }

    private void OnUndoClick(object? sender, RoutedEventArgs? e)
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var restore = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(CaptureSnapshot());
        ApplySnapshot(restore);
        NotifyUndoRedoProperties();
    }

    private void OnRedoClick(object? sender, RoutedEventArgs? e)
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var restore = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(CaptureSnapshot());
        ApplySnapshot(restore);
        NotifyUndoRedoProperties();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        if (e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                OnRedoClick(this, null);
            }
            else
            {
                OnUndoClick(this, null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Y)
        {
            OnRedoClick(this, null);
            e.Handled = true;
        }
    }

    private void PushUndoBeforeMutation()
    {
        if (_undoRedoMuted)
        {
            return;
        }

        _undoStack.Add(CaptureSnapshot());
        TrimUndoStack();
        _redoStack.Clear();
        NotifyUndoRedoProperties();
    }

    private void CommitBaseline(ref WorkspaceSnapshot? baseline)
    {
        if (_undoRedoMuted || baseline is null)
        {
            return;
        }

        var before = baseline;
        baseline = null;
        var now = CaptureSnapshot();
        if (SnapshotsEqual(before, now))
        {
            return;
        }

        _undoStack.Add(before);
        TrimUndoStack();
        _redoStack.Clear();
        NotifyUndoRedoProperties();
    }

    private void TrimUndoStack()
    {
        while (_undoStack.Count > MaxUndoSteps)
        {
            _undoStack.RemoveAt(0);
        }
    }

    private void NotifyUndoRedoProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanUndo)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRedo)));
    }

    private WorkspaceSnapshot CaptureSnapshot()
    {
        var n = DataPoints.Count;
        var px = new string[n];
        var py = new string[n];
        for (var i = 0; i < n; i++)
        {
            px[i] = DataPoints[i].X;
            py[i] = DataPoints[i].Y;
        }

        var m = Predictions.Count;
        var rx = new string[m];
        var ryp = new string[m];
        var ry = new string[m];
        var re = new string[m];
        for (var i = 0; i < m; i++)
        {
            rx[i] = Predictions[i].X;
            ryp[i] = Predictions[i].YPred;
            ry[i] = Predictions[i].Y;
            re[i] = Predictions[i].Error;
        }

        return new WorkspaceSnapshot(
            px,
            py,
            rx,
            ryp,
            ry,
            re,
            Equation,
            Coefficient,
            IntermediateComputations,
            CurrentDatasetName,
            MethodComboBox?.SelectedIndex ?? 0,
            DegreeComboBox?.SelectedIndex ?? 0);
    }

    private void ApplySnapshot(WorkspaceSnapshot s)
    {
        _undoRedoMuted = true;
        try
        {
            _dataEditBaseline = null;
            _titleEditBaseline = null;

            DataPoints.Clear();
            for (var i = 0; i < s.PointX.Length; i++)
            {
                DataPoints.Add(new DataPoint
                {
                    X = s.PointX[i],
                    Y = s.PointY[i]
                });
            }

            Predictions.Clear();
            for (var i = 0; i < s.PredX.Length; i++)
            {
                Predictions.Add(new Prediction
                {
                    X = s.PredX[i],
                    YPred = s.PredYPred[i],
                    Y = s.PredY[i],
                    Error = s.PredErr[i]
                });
            }

            Equation = s.Equation;
            Coefficient = s.Coefficient;
            IntermediateComputations = s.Intermediate;
            CurrentDatasetName = s.DatasetTitle;

            if (MethodComboBox is not null)
            {
                MethodComboBox.SelectedIndex = Math.Clamp(s.MethodIndex, 0, 2);
            }

            if (DegreeComboBox is not null)
            {
                DegreeComboBox.SelectedIndex = Math.Clamp(s.DegreeIndex, 0, 2);
            }

            UpdateDegreeComboBoxVisibility();
            RefreshUserDatasetsList();
        }
        finally
        {
            _undoRedoMuted = false;
        }
    }

    private static bool SnapshotsEqual(WorkspaceSnapshot a, WorkspaceSnapshot b)
    {
        if (a.Equation != b.Equation
            || a.Coefficient != b.Coefficient
            || a.Intermediate != b.Intermediate
            || a.DatasetTitle != b.DatasetTitle
            || a.MethodIndex != b.MethodIndex
            || a.DegreeIndex != b.DegreeIndex)
        {
            return false;
        }

        if (a.PointX.Length != b.PointX.Length)
        {
            return false;
        }

        for (var i = 0; i < a.PointX.Length; i++)
        {
            if (a.PointX[i] != b.PointX[i] || a.PointY[i] != b.PointY[i])
            {
                return false;
            }
        }

        if (a.PredX.Length != b.PredX.Length)
        {
            return false;
        }

        for (var i = 0; i < a.PredX.Length; i++)
        {
            if (a.PredX[i] != b.PredX[i]
                || a.PredYPred[i] != b.PredYPred[i]
                || a.PredY[i] != b.PredY[i]
                || a.PredErr[i] != b.PredErr[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Same loading path as Load Dataset from the menu: refresh from DB then apply.</summary>
    private void TryLoadDatasetFromDb(DatasetRecord? reference)
    {
        if (reference is null)
        {
            return;
        }

        TryLoadDatasetById(reference.Id);
    }

    private void TryLoadDatasetById(int datasetId)
    {
        if (datasetId == _currentDatasetId)
        {
            return;
        }

        var userId = AppServices.Session.CurrentUser?.Id ?? 0;
        var fresh = AppServices.Database.GetDataset(datasetId);
        if (fresh is null || fresh.UserId != userId)
        {
            return;
        }

        LoadDataset(fresh);
    }

    private void PersistDatasetNameOnly()
    {
        var userId = AppServices.Session.CurrentUser?.Id ?? 0;
        if (_currentDatasetId == 0)
        {
            return;
        }

        var dataset = AppServices.Database.GetDataset(_currentDatasetId);
        if (dataset is null || dataset.UserId != userId)
        {
            return;
        }

        var trimmed = CurrentDatasetName.Trim();
        if (string.IsNullOrEmpty(trimmed) || dataset.Name == trimmed)
        {
            return;
        }

        dataset.Name = trimmed;
        AppServices.Database.UpsertDataset(dataset);
        RefreshUserDatasetsList();
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnRestoreDownClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Normal
            ? WindowState.FullScreen
            : WindowState.Normal;
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Normal && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnResizeTopPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.North, e);
    }

    private void OnResizeLeftPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.West, e);
    }

    private void OnResizeRightPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.East, e);
    }

    private void OnResizeBottomPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.South, e);
    }

    private void OnResizeTopLeftPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.NorthWest, e);
    }

    private void OnResizeTopRightPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.NorthEast, e);
    }

    private void OnResizeBottomLeftPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.SouthWest, e);
    }

    private void OnResizeBottomRightPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDragIfPossible(WindowEdge.SouthEast, e);
    }

    private void BeginResizeDragIfPossible(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (WindowState != WindowState.Normal || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginResizeDrag(edge, e);
    }

    private void OnAddRowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PushUndoBeforeMutation();
        DataPoints.Add(new DataPoint());
    }

    private void OnDeleteLastRowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataPoints.Count <= 0)
        {
            return;
        }

        PushUndoBeforeMutation();
        DataPoints.RemoveAt(DataPoints.Count - 1);
    }

    private void CreateBlankDataset()
    {
        var userId = AppServices.Session.CurrentUser?.Id ?? 0;
        var name = $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}";
        var dataset = AppServices.Database.CreateDataset(userId, name);
        LoadDataset(dataset);
    }

    private async System.Threading.Tasks.Task<DatasetRecord?> PickDatasetAsync()
    {
        var userId = AppServices.Session.CurrentUser?.Id ?? 0;
        var datasets = AppServices.Database.GetDatasets(userId);
        var picker = new DatasetPickerWindow(datasets);
        return await picker.ShowDialog<DatasetRecord?>(this);
    }

    private void LoadDataset(DatasetRecord dataset)
    {
        _loadingDataset = true;
        try
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _dataEditBaseline = null;
            _titleEditBaseline = null;
            NotifyUndoRedoProperties();

            _currentDatasetId = dataset.Id;
            CurrentDatasetName = dataset.Name ?? "";

            DataPoints.Clear();
            foreach (var point in dataset.Points ?? [])
            {
                DataPoints.Add(new DataPoint
                {
                    X = point.X,
                    Y = point.Y
                });
            }

            if (DataPoints.Count == 0)
            {
                for (var index = 0; index < 3; index++)
                {
                    DataPoints.Add(new DataPoint());
                }
            }

            Predictions.Clear();
            foreach (var prediction in dataset.Predictions ?? [])
            {
                Predictions.Add(new Prediction
                {
                    X = prediction.X,
                    YPred = prediction.YPred,
                    Y = prediction.Y,
                    Error = prediction.Error
                });
            }

            Equation = dataset.Equation ?? "No calculation yet";
            Coefficient = dataset.Coefficient ?? "-";
            IntermediateComputations = dataset.IntermediateComputations ?? "-";

            RefreshUserDatasetsList();
        }
        finally
        {
            _loadingDataset = false;
        }
    }

    private void RefreshUserDatasetsList()
    {
        _suppressDatasetListSelection = true;
        try
        {
            var userId = AppServices.Session.CurrentUser?.Id ?? 0;
            var fromDb = AppServices.Database.GetDatasets(userId);

            UserDatasets.Clear();
            foreach (var record in fromDb)
            {
                UserDatasets.Add(record);
            }

            if (DatasetListBox is not null)
            {
                DatasetListBox.SelectedItem =
                    UserDatasets.FirstOrDefault(dataset => dataset.Id == _currentDatasetId);
            }
        }
        finally
        {
            _suppressDatasetListSelection = false;
        }
    }

    private static readonly FilePickerFileType PdfFileType = new("PDF")
    {
        Patterns = ["*.pdf"],
        MimeTypes = ["application/pdf"]
    };

    private async void OnExportDatasetClick(object? sender, RoutedEventArgs e)
    {
        var previousWindowState = WindowState;
        if (previousWindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
        }

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider.CanSave != true)
            {
                await MessageWindow.ShowAsync(this, "Export unavailable", "Saving files is not supported on this platform.");
                return;
            }

            Activate();

            var safeName = SanitizeFileName(CurrentDatasetName.Trim());
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "dataset";
            }

            var options = new FilePickerSaveOptions
            {
                Title = "Export Analysis Report",
                SuggestedFileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                DefaultExtension = "pdf",
                ShowOverwritePrompt = true,
                FileTypeChoices = [PdfFileType]
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file is null)
            {
                return;
            }

            var dataset = BuildDatasetRecordForExport();
            var exportService = new ExportService();
            var pdfBytes = exportService.ExportDatasetToPdf(dataset);

            await using (var stream = await file.OpenWriteAsync())
            {
                await stream.WriteAsync(pdfBytes);
                await stream.FlushAsync();
            }

            await MessageWindow.ShowAsync(
                this,
                "Export complete",
                $"Your analysis report was saved as:\n{file.Name}");
        }
        catch (Exception ex)
        {
            await MessageWindow.ShowAsync(
                this,
                "Export failed",
                $"Could not create the PDF report.\n\n{ex.Message}");
        }
        finally
        {
            WindowState = previousWindowState;
        }
    }

    private DatasetRecord BuildDatasetRecordForExport()
    {
        var userId = AppServices.Session.CurrentUser?.Id ?? 0;
        return new DatasetRecord
        {
            Id = _currentDatasetId,
            UserId = userId,
            Name = CurrentDatasetName.Trim(),
            Equation = Equation,
            Coefficient = Coefficient,
            IntermediateComputations = IntermediateComputations,
            Points = DataPoints
                .Select(point => new DatasetPointRecord
                {
                    X = point.X,
                    Y = point.Y
                })
                .ToList(),
            Predictions = Predictions
                .Select(prediction => new PredictionRecord
                {
                    X = prediction.X,
                    YPred = prediction.YPred,
                    Y = prediction.Y,
                    Error = prediction.Error
                })
                .ToList()
        };
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }

    private void SaveCurrentDataset()

    {
        var userId = AppServices.Session.CurrentUser?.Id ?? 0;

        if (_currentDatasetId == 0)
        {
            var trimmed = CurrentDatasetName.Trim();
            var name = string.IsNullOrEmpty(trimmed)
                ? $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}"
                : trimmed;
            var created = AppServices.Database.CreateDataset(userId, name);
            _currentDatasetId = created.Id;
            CurrentDatasetName = created.Name;
        }

        var dataset = AppServices.Database.GetDataset(_currentDatasetId);
        if (dataset is null || dataset.UserId != userId)
        {
            return;
        }

        var nameTrimmed = CurrentDatasetName.Trim();
        if (!string.IsNullOrEmpty(nameTrimmed))
        {
            dataset.Name = nameTrimmed;
        }

        dataset.Points = DataPoints
            .Select(point => new DatasetPointRecord
            {
                X = point.X,
                Y = point.Y
            })
            .ToList();
        dataset.Equation = Equation;
        dataset.Coefficient = Coefficient;
        dataset.IntermediateComputations = IntermediateComputations;
        dataset.Predictions = Predictions
            .Select(prediction => new PredictionRecord
            {
                X = prediction.X,
                YPred = prediction.YPred,
                Y = prediction.Y,
                Error = prediction.Error
            })
            .ToList();

        AppServices.Database.UpsertDataset(dataset);
        RefreshUserDatasetsList();
    }

    private void OnMethodSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDegreeComboBoxVisibility();
    }

    private void UpdateDegreeComboBoxVisibility()
    {
        if (DegreeComboBox is null)
        {
            return;
        }

        var usesDegree = MethodComboBox.SelectedIndex is 1 or 2;
        DegreeComboBox.IsEnabled = usesDegree;
        DegreeComboBox.IsVisible = usesDegree;
    }

    private void OnCalculateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PushUndoBeforeMutation();

        var points = ParsePoints();
        if (points.Count < 2)
        {
            Equation = "Enter at least two valid X/Y rows";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        var methodIndex = MethodComboBox.SelectedIndex;
        var degree = DegreeComboBox.SelectedIndex + 2;

        if (methodIndex == 1)
        {
            CalculatePolynomial(points, degree);
            return;
        }

        if (methodIndex == 2)
        {
            CalculateBoth(points, degree);
            return;
        }

        CalculateLinear(points);
    }

    private List<(double X, double Y)> ParsePoints()
    {
        return DataPoints
            .Select(point => (
                ParsedX: double.TryParse(point.X, NumberStyles.Float, CultureInfo.InvariantCulture, out var x),
                X: x,
                ParsedY: double.TryParse(point.Y, NumberStyles.Float, CultureInfo.InvariantCulture, out var y),
                Y: y))
            .Where(point => point.ParsedX && point.ParsedY)
            .Select(point => (point.X, point.Y))
            .ToList();
    }

    private void CalculateLinear(IReadOnlyList<(double X, double Y)> points)
    {
        var averageX = points.Average(point => point.X);
        var averageY = points.Average(point => point.Y);
        var denominator = points.Sum(point => Math.Pow(point.X - averageX, 2));

        if (Math.Abs(denominator) < double.Epsilon)
        {
            Equation = "Cannot calculate: X values must not all be equal";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        var (intercept, slope) = FitLinear(points, averageX, averageY, denominator);

        UpdateResults(
            points,
            point => intercept + slope * point.X,
            $"y = {slope:F4}x + {intercept:F4}",
            $"Intercept = {intercept:F4}{Environment.NewLine}Slope = {slope:F4}");
    }

    private void CalculatePolynomial(IReadOnlyList<(double X, double Y)> points, int degree)
    {
        if (points.Count <= degree)
        {
            Equation = $"Enter at least {degree + 1} valid rows for degree {degree}";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        var coefficients = FitPolynomial(points, degree);
        if (coefficients is null)
        {
            Equation = "Cannot calculate: polynomial system is singular";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        UpdateResults(
            points,
            point => EvaluatePolynomial(coefficients, point.X),
            FormatPolynomialEquation(coefficients),
            FormatCoefficients(coefficients));
    }

    private void CalculateBoth(IReadOnlyList<(double X, double Y)> points, int degree)
    {
        var averageX = points.Average(point => point.X);
        var averageY = points.Average(point => point.Y);
        var denominator = points.Sum(point => Math.Pow(point.X - averageX, 2));

        if (Math.Abs(denominator) < double.Epsilon)
        {
            Equation = "Cannot calculate: X values must not all be equal";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        if (points.Count <= degree)
        {
            Equation = $"Enter at least {degree + 1} valid rows for degree {degree}";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        var (intercept, slope) = FitLinear(points, averageX, averageY, denominator);
        var coefficients = FitPolynomial(points, degree);
        if (coefficients is null)
        {
            Equation = "Cannot calculate: polynomial system is singular";
            Coefficient = "-";
            IntermediateComputations = "-";
            Predictions.Clear();
            return;
        }

        UpdateResults(
            points,
            point => EvaluatePolynomial(coefficients, point.X),
            $"Linear: y = {slope:F4}x + {intercept:F4}{Environment.NewLine}Polynomial: {FormatPolynomialEquation(coefficients)}",
            $"Linear:{Environment.NewLine}Intercept = {intercept:F4}{Environment.NewLine}Slope = {slope:F4}{Environment.NewLine}{Environment.NewLine}Polynomial:{Environment.NewLine}{FormatCoefficients(coefficients)}");
    }

    private static (double Intercept, double Slope) FitLinear(
        IReadOnlyList<(double X, double Y)> points,
        double averageX,
        double averageY,
        double denominator)
    {
        var slope = points.Sum(point => (point.X - averageX) * (point.Y - averageY)) / denominator;
        var intercept = averageY - slope * averageX;
        return (intercept, slope);
    }

    private static double[]? FitPolynomial(IReadOnlyList<(double X, double Y)> points, int degree)
    {
        var size = degree + 1;
        var matrix = new double[size, size + 1];

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                matrix[row, column] = points.Sum(point => Math.Pow(point.X, row + column));
            }

            matrix[row, size] = points.Sum(point => point.Y * Math.Pow(point.X, row));
        }

        for (var pivot = 0; pivot < size; pivot++)
        {
            var bestRow = pivot;
            for (var row = pivot + 1; row < size; row++)
            {
                if (Math.Abs(matrix[row, pivot]) > Math.Abs(matrix[bestRow, pivot]))
                {
                    bestRow = row;
                }
            }

            if (Math.Abs(matrix[bestRow, pivot]) < 1e-12)
            {
                return null;
            }

            if (bestRow != pivot)
            {
                for (var column = pivot; column <= size; column++)
                {
                    (matrix[pivot, column], matrix[bestRow, column]) =
                        (matrix[bestRow, column], matrix[pivot, column]);
                }
            }

            var pivotValue = matrix[pivot, pivot];
            for (var column = pivot; column <= size; column++)
            {
                matrix[pivot, column] /= pivotValue;
            }

            for (var row = 0; row < size; row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                var factor = matrix[row, pivot];
                for (var column = pivot; column <= size; column++)
                {
                    matrix[row, column] -= factor * matrix[pivot, column];
                }
            }
        }

        var coefficients = new double[size];
        for (var row = 0; row < size; row++)
        {
            coefficients[row] = matrix[row, size];
        }

        return coefficients;
    }

    private static double EvaluatePolynomial(IReadOnlyList<double> coefficients, double x)
    {
        var result = 0d;
        for (var index = coefficients.Count - 1; index >= 0; index--)
        {
            result = result * x + coefficients[index];
        }

        return result;
    }

    private static string FormatPolynomialEquation(IReadOnlyList<double> coefficients)
    {
        var terms = new List<string>();
        for (var index = coefficients.Count - 1; index >= 0; index--)
        {
            var coefficient = coefficients[index];
            var term = index switch
            {
                0 => $"{coefficient:F4}",
                1 => $"{coefficient:F4}x",
                _ => $"{coefficient:F4}x^{index}"
            };

            terms.Add(term);
        }

        return $"y = {string.Join(" + ", terms)}";
    }

    private static string FormatCoefficients(IReadOnlyList<double> coefficients)
    {
        var parts = new List<string>();
        for (var index = 0; index < coefficients.Count; index++)
        {
            parts.Add($"a{index} = {coefficients[index]:F4}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private void UpdateResults(
        IReadOnlyList<(double X, double Y)> points,
        Func<(double X, double Y), double> predict,
        string equation,
        string coefficient)
    {
        var averageY = points.Average(point => point.Y);
        var totalSumSquares = points.Sum(point => Math.Pow(point.Y - averageY, 2));
        var sumSquaredErrors = points.Sum(point =>
        {
            var predictedY = predict(point);
            return Math.Pow(point.Y - predictedY, 2);
        });

        Predictions.Clear();
        foreach (var point in points)
        {
            var predictedY = predict(point);
            Predictions.Add(new Prediction
            {
                X = point.X.ToString("G6", CultureInfo.InvariantCulture),
                YPred = predictedY.ToString("F4", CultureInfo.InvariantCulture),
                Y = point.Y.ToString("G6", CultureInfo.InvariantCulture),
                Error = (point.Y - predictedY).ToString("F4", CultureInfo.InvariantCulture)
            });
        }

        Equation = equation;
        Coefficient = coefficient;
        var rSquared = totalSumSquares <= double.Epsilon
            ? "-"
            : (1 - sumSquaredErrors / totalSumSquares).ToString("F4", CultureInfo.InvariantCulture);
        var meanSquaredError = sumSquaredErrors / points.Count;
        IntermediateComputations =
            $"R^2 = {rSquared}{Environment.NewLine}SSE = {sumSquaredErrors.ToString("F4", CultureInfo.InvariantCulture)}{Environment.NewLine}MSE = {meanSquaredError.ToString("F4", CultureInfo.InvariantCulture)}";
        SaveCurrentDataset();
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record WorkspaceSnapshot(
        string[] PointX,
        string[] PointY,
        string[] PredX,
        string[] PredYPred,
        string[] PredY,
        string[] PredErr,
        string Equation,
        string Coefficient,
        string Intermediate,
        string DatasetTitle,
        int MethodIndex,
        int DegreeIndex);
}
