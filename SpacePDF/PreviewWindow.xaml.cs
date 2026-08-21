using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using SpacePDF.Tools;

namespace SpacePDF;

public partial class PreviewWindow : Window
{
    private string pdfPath;
    private bool _isDragging = false;
    private Point startPoint;
    private Point endPoint;
    private int dragPageIndex;
    public PreviewWindow(string pdfFilePath,int DPI)
    {
        InitializeComponent();
        pdfPath = pdfFilePath;
        LoadPdfPreview(pdfPath, DPI);
    }

    private async Task LoadPdfPreview(string pdfPath,int DPI)
    {
        await Task.Run(() =>
        {
            using var document = PdfiumViewer.PdfDocument.Load(pdfPath);
            int pageCount = document.PageCount;
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) 
            {
                var bitMapSource = PdfRenderHelper.RenderPageToBitmap(pdfPath, pageIndex,DPI);
                //在UI上添加控件
                Dispatcher.Invoke(new Action(() => 
                {
                    var imageControl = new Image
                    {
                        Source = bitMapSource,
                        Margin = new Thickness(0,10,0,10),
                        Tag = pageIndex,
                        
                    };
                    imageControl.Width = 800;
                    imageControl.MouseLeftButtonDown += PdfImage_MouseLeftButtonDown;
                    imageControl.MouseLeftButtonUp += PdfImage_MouseLeftButtonUp;
                    imageControl.MouseMove += PdfImage_MouseMove;

                    PagesContainer.Children.Add(imageControl);

                    var brushConverter = new BrushConverter();
                    var rectangle = new Rectangle
                    {
                        Fill = (Brush)brushConverter.ConvertFromString("#33FF0000"),
                    };

                    var selectionCanvas = new Canvas
                    {
                        IsHitTestVisible = false,
                    };
                    selectionCanvas.Children.Add(rectangle);
                }));
                
            }
            Dispatcher.Invoke(new Action(() => 
            {
                ProgressBar.IsIndeterminate = false;
            }));
        });
    }

    private void PdfImage_MouseMove(object sender, MouseEventArgs e)
    {
        // 更新提示用矩形
        if (sender is not Image clickedImage || !_isDragging) return;
        Point currentPoint = e.GetPosition(clickedImage);

    }

    private void PdfImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || sender is not Image clickedImage) return;

        endPoint = e.GetPosition(clickedImage);
        clickedImage.ReleaseMouseCapture();
        _isDragging = false;
        PdfImage_Complete(startPoint,endPoint);
    }

    private void PdfImage_Complete(Point start, Point end)
    {
        MessageBox.Show("start y=" + start.Y + "\nend y=" + end.Y + "\npageIndex" + dragPageIndex); 
    }

    private void PdfImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image clickedImage) return;

        dragPageIndex = (int)clickedImage.Tag;
        startPoint = e.GetPosition(clickedImage);
        _isDragging = true;
        clickedImage.CaptureMouse();
    }
}
