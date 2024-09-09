using System.Windows;

namespace DevModManager.App
{
    public partial class MissingConfigDialog : Window
    {
        public enum DialogResult
        {
            CopySample,
            SettingsWindow,
            Exit,
            None
        }

        public DialogResult Result { get; private set; } = DialogResult.None;

        public MissingConfigDialog()
        {
            InitializeComponent();
        }


        private void CopySample_Click(object sender, RoutedEventArgs e)
        {
            Result = DialogResult.CopySample;
            Close();
        }

        private void SettingsWindow_Click(object sender, RoutedEventArgs e)
        {
            Result = DialogResult.SettingsWindow;
            Close();
        }
        
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Result = DialogResult.Exit;
            Close();
        }
    }
}
