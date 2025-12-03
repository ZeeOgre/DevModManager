using Avalonia.Controls;
using DevModManager.Core.ViewModels;

namespace DevModManager.Avalonia
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }
}
