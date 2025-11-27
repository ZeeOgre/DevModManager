using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace ZO.DMM.AppNF
{
    public partial class UrlInputDialog : Window
    {
        private readonly ModItem _modItem; // Add a private field for ModItem
        public string ModName { get; }
        public string Url { get; set; }
        public Uri UrlTypeLink { get; }
        public string UrlType { get; }

        public UrlInputDialog(ModItem modItem, string urlType)
        {
            InitializeComponent();
            _modItem = modItem; // Store the ModItem instance
            ModName = modItem.ModName;
            UrlType = urlType;
            System.Windows.Clipboard.SetText(ModName);

            // Set UrlTypeLink based on urlType
            if (urlType == "Nexus")
            {
                UrlTypeLink = new Uri("https://www.nexusmods.com/starfield/mods");
            }
            else if (urlType == "Bethesda")
            {
                UrlTypeLink = new Uri("https://creations.bethesda.net/en/starfield");
            }
            else
            {
                UrlTypeLink = new Uri("about:blank");
            }

            DataContext = this;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Url = UrlTextBox.Text; // Correct reference to UrlTextBox
            if (UrlType == "Nexus")
            {
                _modItem.NexusUrl = Url; // Update NexusUrl in ModItem
            }
            else if (UrlType == "Bethesda")
            {
                _modItem.BethesdaUrl = Url; // Update BethesdaUrl in ModItem
            }
            ModItem.DB.WriteMod(_modItem); // Write the updated ModItem to the database 
            DialogResult = true;
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            _ = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
