using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SpacePDF;

public partial class PreviewWindow : Window
{

    public PreviewWindow(string pdfFilePath)
    {
        InitializeComponent();

    }

}
