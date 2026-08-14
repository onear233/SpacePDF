using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace SpacePDF;

public partial class MainWindow : Window
{
    private string? _selectedFilePath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectPdfButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select PDF File",
            Filter = "PDF Files (*.pdf)|*.pdf",
            DefaultExt = ".pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        if (!IsValidPdf(dialog.FileName))
        {
            MessageBox.Show("The selected file is not a valid PDF or cannot be read.",
                "Invalid PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
            _selectedFilePath = dialog.FileName;
            FilePathText.Text = dialog.FileName;
            OpenPreviewButton.IsEnabled = true;
    }

    private int _cumulativeVersion;

  
    private static bool IsValidPdf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[5];
            if (stream.Read(header, 0, 5) < 5)
                return false;
            return header.AsSpan().SequenceEqual("%PDF-"u8);
        }
        catch
        {
            return false;
        }
    }

    private void OpenPreviewButton_Click(object sender, RoutedEventArgs e)
    {

    }
}
