using PdfiumViewer;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace SpacePDF.Tools
{
    /// <summary>
    /// 将PDF转换为可用的BimapSource，一页一页的处理
    /// </summary>
    class PdfRenderHelper
    {
       public static BitmapSource RenderPageToBitmap(string pdfPath, int pageIndex,int dpi = 100)
        {
            //自动释放的document对象
            using var document = PdfDocument.Load(pdfPath);
            var pageSize = document.PageSizes[pageIndex];
            

            int width = (int)(pageSize.Width /72.0 *dpi);
            int height = (int)(pageSize.Height /72.0 *dpi);

            using var image = document.Render(pageIndex, width, height,dpi,dpi,PdfRenderFlags.CorrectFromDpi);

            return ConvertBitmapToBitmapSource((Bitmap)image);
        }

        private static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            // 保存为 PNG 保证无损质量
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 彻底加载到内存，避免文件占用
            bitmapImage.EndInit();
            bitmapImage.Freeze(); // 冻结对象，跨线程可用且提高性能

            return bitmapImage;
        }
    }
}
