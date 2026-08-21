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
                        Tag = pageIndex
                    };
                    imageControl.Width = 800;
                    PagesContainer.Children.Add(imageControl);
                }));
                
            }
            Dispatcher.Invoke(new Action(() => 
            {
                ProgressBar.IsIndeterminate = false;
            }));
        });
    }
}
