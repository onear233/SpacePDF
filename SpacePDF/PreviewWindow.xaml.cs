using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using SpacePDF.Models;
using SpacePDF.Services;

namespace SpacePDF;

public partial class PreviewWindow : Window
{
    private readonly string _pdfFilePath;
    private readonly PdfRenderService _renderService = new();
    private readonly PdfExportService _exportService = new();
    private DocumentManager? _docManager;

    private double _zoomFactor = 1.0;
    private const double MinZoom = 0.15;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 1.15;

    // Drag state
    private bool _isDragging;
    private double _dragStartY;
    private Rectangle? _dragRect;

    private int _versionNumber;
    public int Version => _versionNumber;

    public PreviewWindow(string pdfFilePath)
    {
        InitializeComponent();
        _pdfFilePath = pdfFilePath;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Rendering PDF...";
            Mouse.OverrideCursor = Cursors.Wait;

            var (images, pageSizes) = _renderService.RenderPdf(_pdfFilePath);

            _docManager = new DocumentManager(
                pageSizes[0].Width, pageSizes[0].Height,
                PdfRenderService.DefaultDpi);

            for (int i = 0; i < images.Count; i++)
                _docManager.AddOriginalPage(images[i], i);

            _docManager.ReflowAndBuildDisplay();
            RefreshBothPanels();
            Dispatcher.BeginInvoke(UpdateContentGridSize, DispatcherPriority.Loaded);
            StatusText.Text =
                $"Loaded {_docManager.Pages.Count} page(s). Ctrl+Scroll to zoom. Drag in the right panel to insert blank space.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error loading PDF.";
            MessageBox.Show($"Failed to render PDF:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    // ── ContentGrid sizing for RenderTransform scrollbar support ─────

    private void UpdateContentGridSize()
    {
        if (ScaleTarget.ActualWidth < 1 || ScaleTarget.ActualHeight < 1)
            return;
        ContentGrid.Width = ScaleTarget.ActualWidth * _zoomFactor;
        ContentGrid.Height = ScaleTarget.ActualHeight * _zoomFactor;
    }

    private void RefreshBothPanels()
    {
        if (_docManager == null) return;

        PdfPreviewControl.ItemsSource = null;
        PdfPreviewControl.ItemsSource = _docManager.Pages;

        InteractionPagesControl.ItemsSource = null;
        InteractionPagesControl.ItemsSource = _docManager.Pages;
    }

    // ── Interaction Panel drag handlers ──────────────────────────────
    // Coordinates are in unzoomed layout pixels. RenderTransform on
    // ScaleTarget is purely visual — it doesn't affect layout or
    // GetPosition results. We convert to points via RenderDpi.

    private void InteractionRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_docManager == null) return;

        _isDragging = true;
        _dragStartY = e.GetPosition(InteractionRoot).Y;

        _dragRect = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(50, 59, 130, 246)),
            Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Width = 220
        };
        Canvas.SetLeft(_dragRect, 0);
        Canvas.SetTop(_dragRect, _dragStartY);
        _dragRect.Height = 0;
        SelectionCanvas.Children.Add(_dragRect);
        InteractionRoot.CaptureMouse();
    }

    private void InteractionRoot_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _dragRect == null) return;

        double currentY = e.GetPosition(InteractionRoot).Y;
        double y = Math.Min(_dragStartY, currentY);
        double h = Math.Abs(currentY - _dragStartY);

        Canvas.SetTop(_dragRect, y);
        _dragRect.Height = h;
    }

    private void InteractionRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _dragRect == null || _docManager == null) return;

        InteractionRoot.ReleaseMouseCapture();
        SelectionCanvas.Children.Remove(_dragRect);
        _dragRect = null;
        _isDragging = false;

        double currentY = e.GetPosition(InteractionRoot).Y;

        // Clamp to the interaction panel's actual rendered height
        double panelHeight = InteractionRoot.ActualHeight;
        double clampedStart = Math.Clamp(_dragStartY, 0, panelHeight);
        double clampedEnd = Math.Clamp(currentY, 0, panelHeight);

        double localMinY = Math.Min(clampedStart, clampedEnd);
        double localMaxY = Math.Max(clampedStart, clampedEnd);
        double rangeHeightPx = localMaxY - localMinY;

        const double minBlankPx = 5.0;
        if (rangeHeightPx < minBlankPx)
            return;

        double startPt = localMinY / _docManager.RenderDpi * 72.0;
        double endPt = localMaxY / _docManager.RenderDpi * 72.0;

        if (clampedEnd > clampedStart)
        {
            // Drag downward — insert blank space
            _docManager.InsertBlank(startPt, endPt - startPt);
            _versionNumber++;
            StatusText.Text = $"Inserted blank ({endPt - startPt:F1} pt) — {_docManager.Pages.Count} page(s)";
        }
        else
        {
            // Drag upward — delete content in range
            _docManager.DeleteRange(startPt, endPt);
            _versionNumber++;
            StatusText.Text = $"Deleted ({endPt - startPt:F1} pt) — {_docManager.Pages.Count} page(s)";
        }

        _docManager.ReflowAndBuildDisplay();
        RefreshBothPanels();

        VersionText.Text = $"v{_versionNumber}";

        Dispatcher.BeginInvoke(UpdateContentGridSize, DispatcherPriority.Loaded);
    }

    // ── Zoom ─────────────────────────────────────────────────────────

    private void PdfScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double newZoom = e.Delta > 0
                ? _zoomFactor * ZoomStep
                : _zoomFactor / ZoomStep;
            ApplyZoom(newZoom);
            e.Handled = true;
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
        => ApplyZoom(_zoomFactor * ZoomStep);

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
        => ApplyZoom(_zoomFactor / ZoomStep);

    private void ApplyZoom(double newZoom)
    {
        _zoomFactor = Math.Clamp(newZoom, MinZoom, MaxZoom);
        ZoomTransform.ScaleX = _zoomFactor;
        ZoomTransform.ScaleY = _zoomFactor;
        UpdateContentGridSize();
        ZoomText.Text = $"{_zoomFactor * 100:F0}%";
    }

    // ── Save ─────────────────────────────────────────────────────────

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        if (_docManager == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Save Modified PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = System.IO.Path.GetFileNameWithoutExtension(_pdfFilePath) + "_modified.pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _exportService.Export(_docManager, dialog.FileName);
            StatusText.Text = $"Saved to {dialog.FileName}";
            MessageBox.Show("PDF saved successfully.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save PDF:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }
}

public class BlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate ImageSliceTemplate { get; set; } = null!;
    public DataTemplate BlankSpaceTemplate { get; set; } = null!;

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            DocumentBlock { Type: BlockType.OriginalPdfContent } => ImageSliceTemplate,
            DocumentBlock { Type: BlockType.UserInsertedBlank } => BlankSpaceTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
