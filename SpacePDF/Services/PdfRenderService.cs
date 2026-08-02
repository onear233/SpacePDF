using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using PdfiumViewer;

namespace SpacePDF.Services;

public class PdfRenderService
{
    public const double DefaultDpi = 200.0;

    public (List<BitmapSource> Images, List<System.Drawing.SizeF> PageSizes) RenderPdf(
        string filePath, double dpi = DefaultDpi)
    {
        using var document = PdfDocument.Load(filePath);
        var images = new List<BitmapSource>(document.PageCount);
        var pageSizes = new List<System.Drawing.SizeF>(document.PageCount);

        for (int i = 0; i < document.PageCount; i++)
        {
            var size = document.PageSizes[i];
            pageSizes.Add(size);

            int widthPx = (int)(size.Width / 72.0 * dpi);
            int heightPx = (int)(size.Height / 72.0 * dpi);

            using var pageImage = document.Render(i, widthPx, heightPx,
                (float)dpi, (float)dpi, PdfRenderFlags.None);

            var bitmapSource = ConvertToBitmapSource(pageImage);
            bitmapSource.Freeze();
            images.Add(bitmapSource);
        }

        return (images, pageSizes);
    }

    private static BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
    {
        using var memoryStream = new MemoryStream();
        image.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        return bitmapImage;
    }
}
