using System.IO;
using System.Windows.Media.Imaging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SpacePDF.Models;

namespace SpacePDF.Services;

public class PdfExportService
{
    public void Export(DocumentManager docManager, string outputPath)
    {
        var document = new PdfDocument();
        double pageWidthPt = docManager.PageWidthPt;
        double pageHeightPt = docManager.PageHeightPt;
        double renderDpi = docManager.RenderDpi;

        foreach (var page in docManager.Pages)
        {
            var pdfPage = document.AddPage();
            pdfPage.Width = pageWidthPt;
            pdfPage.Height = pageHeightPt;
            using var gfx = XGraphics.FromPdfPage(pdfPage);

            double currentYPt = 0;
            foreach (var block in page.Blocks)
            {
                if (block.Type == BlockType.OriginalPdfContent && block.Image != null)
                {
                    var pngBytes = EncodeToPng(block.Image);
                    var xImage = XImage.FromStream(() => new MemoryStream(pngBytes));
                    double blockWidthPt = block.DisplayWidthPx / renderDpi * 72.0;
                    gfx.DrawImage(xImage, 0, currentYPt, blockWidthPt, block.Height);
                }
                currentYPt += block.Height;
            }
        }

        document.Save(outputPath);
    }

    private static byte[] EncodeToPng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
