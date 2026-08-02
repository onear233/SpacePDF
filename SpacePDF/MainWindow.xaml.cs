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

    private void OpenPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath) || !IsValidPdf(_selectedFilePath))
            return;

        var previewWindow = new PreviewWindow(_selectedFilePath);
        previewWindow.Owner = this;
        previewWindow.ShowDialog();

        if (previewWindow.Version > 0)
        {
            _cumulativeVersion += previewWindow.Version;
            VersionLabel.Text = $"Total blanks inserted: {_cumulativeVersion}";
        }
    }

    private static bool IsValidPdf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[5];
            if (stream.Read(header, 0, 5) < 5)
                return false;
            return header[0] == '%' && header[1] == 'P' && header[2] == 'D'
                && header[3] == 'F' && header[4] == '-';
        }
        catch
        {
            return false;
        }
    }
}
