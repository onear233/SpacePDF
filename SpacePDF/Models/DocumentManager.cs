using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpacePDF.Models;

public enum BlockType { OriginalPdfContent, UserInsertedBlank }

public class DocumentBlock
{
    public BlockType Type { get; set; }
    public double Height { get; set; }
    public int SourcePageIndex { get; set; }
    public double SourceCropY { get; set; }

    // Display properties set by BuildDisplayData
    public BitmapSource? ImageSource { get; set; }
    public CroppedBitmap? Image { get; set; }
    public double DisplayHeightPx { get; set; }
    public double DisplayWidthPx { get; set; }
    public double YPositionPx { get; set; }
}

public class PageModel
{
    public int PageIndex { get; set; }
    public double OriginalHeight { get; set; }
    public List<DocumentBlock> Blocks { get; set; } = new();

    public double DisplayWidthPx { get; set; }
    public double DisplayHeightPx { get; set; }
}

public class DocumentManager
{
    public double PageWidthPt { get; }
    public double PageHeightPt { get; }
    public double RenderDpi { get; }

    private readonly Dictionary<int, BitmapSource> _originalImages = new();
    public List<DocumentBlock> GlobalBlocks { get; } = new();
    public List<PageModel> Pages { get; private set; } = new();

    public double PageWidthPx => PageWidthPt / 72.0 * RenderDpi;
    public double PageHeightPx => PageHeightPt / 72.0 * RenderDpi;

    public DocumentManager(double pageWidthPt, double pageHeightPt, double renderDpi)
    {
        PageWidthPt = pageWidthPt;
        PageHeightPt = pageHeightPt;
        RenderDpi = renderDpi;
    }

    public void AddOriginalPage(BitmapSource pageImage, int pageIndex)
    {
        _originalImages[pageIndex] = pageImage;
        double heightPt = pageImage.PixelHeight / RenderDpi * 72.0;
        GlobalBlocks.Add(new DocumentBlock
        {
            Type = BlockType.OriginalPdfContent,
            Height = heightPt,
            SourcePageIndex = pageIndex,
            SourceCropY = 0
        });
    }

    /// <param name="globalYPt">Y position in points from top of document.</param>
    /// <param name="blankHeightPt">Height of blank to insert in points.</param>
    public void InsertBlank(double globalYPt, double blankHeightPt)
    {
        if (blankHeightPt < 2.0)
            return;

        double accumulated = 0;
        for (int i = 0; i < GlobalBlocks.Count; i++)
        {
            var block = GlobalBlocks[i];
            double blockEnd = accumulated + block.Height;

            if (globalYPt < accumulated - 0.5 || globalYPt > blockEnd + 0.5)
            {
                accumulated = blockEnd;
                continue;
            }

            double relativeY = globalYPt - accumulated;

            if (block.Type == BlockType.UserInsertedBlank)
            {
                block.Height += blankHeightPt;
                return;
            }

            if (relativeY <= 1.0)
            {
                GlobalBlocks.Insert(i, NewBlank(blankHeightPt));
            }
            else if (relativeY >= block.Height - 1.0)
            {
                GlobalBlocks.Insert(i + 1, NewBlank(blankHeightPt));
            }
            else
            {
                double topH = relativeY;
                double bottomH = block.Height - relativeY;

                GlobalBlocks.RemoveAt(i);
                GlobalBlocks.Insert(i, new DocumentBlock
                {
                    Type = BlockType.OriginalPdfContent,
                    Height = bottomH,
                    SourcePageIndex = block.SourcePageIndex,
                    SourceCropY = block.SourceCropY + topH
                });
                GlobalBlocks.Insert(i, NewBlank(blankHeightPt));
                GlobalBlocks.Insert(i, new DocumentBlock
                {
                    Type = BlockType.OriginalPdfContent,
                    Height = topH,
                    SourcePageIndex = block.SourcePageIndex,
                    SourceCropY = block.SourceCropY
                });
            }
            return;
        }

        // Beyond all blocks — append at end
        GlobalBlocks.Add(NewBlank(blankHeightPt));
    }

    public void ReflowAndBuildDisplay()
    {
        ReflowPages();
        BuildDisplayData();
    }

    private void ReflowPages()
    {
        Pages = new List<PageModel>();
        var currentPage = new PageModel { PageIndex = 0, OriginalHeight = PageHeightPt };
        double currentYPt = 0;

        foreach (var globalBlock in GlobalBlocks)
        {
            double remaining = globalBlock.Height;
            double cropOffset = 0;

            while (remaining > 0.01)
            {
                double spaceLeft = PageHeightPt - currentYPt;

                if (spaceLeft < 0.5)
                {
                    Pages.Add(currentPage);
                    currentPage = new PageModel { PageIndex = Pages.Count, OriginalHeight = PageHeightPt };
                    currentYPt = 0;
                    spaceLeft = PageHeightPt;
                }

                double portion = Math.Min(remaining, spaceLeft);

                currentPage.Blocks.Add(new DocumentBlock
                {
                    Type = globalBlock.Type,
                    Height = portion,
                    SourcePageIndex = globalBlock.SourcePageIndex,
                    SourceCropY = globalBlock.SourceCropY + cropOffset
                });

                currentYPt += portion;
                remaining -= portion;
                cropOffset += portion;
            }
        }

        if (currentPage.Blocks.Count > 0)
            Pages.Add(currentPage);
    }

    private void BuildDisplayData()
    {
        foreach (var page in Pages)
        {
            page.DisplayWidthPx = PageWidthPx;
            page.DisplayHeightPx = PageHeightPx;

            double yPx = 0;
            foreach (var block in page.Blocks)
            {
                block.DisplayWidthPx = PageWidthPx;
                block.DisplayHeightPx = block.Height / 72.0 * RenderDpi;
                block.YPositionPx = yPx;
                yPx += block.DisplayHeightPx;

                if (block.Type == BlockType.OriginalPdfContent
                    && _originalImages.TryGetValue(block.SourcePageIndex, out var sourceImage))
                {
                    block.ImageSource = sourceImage;
                    int srcYPx = (int)Math.Round(block.SourceCropY / 72.0 * RenderDpi);
                    int cropHPx = (int)Math.Round(block.Height / 72.0 * RenderDpi);
                    srcYPx = Math.Clamp(srcYPx, 0, sourceImage.PixelHeight - 1);
                    cropHPx = Math.Clamp(cropHPx, 1, sourceImage.PixelHeight - srcYPx);
                    block.Image = new CroppedBitmap(sourceImage,
                        new Int32Rect(0, srcYPx, sourceImage.PixelWidth, cropHPx));
                }
            }
        }
    }

    private static DocumentBlock NewBlank(double heightPt) => new()
    {
        Type = BlockType.UserInsertedBlank,
        Height = heightPt
    };
}
