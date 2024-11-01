using System.Windows;
using System.Windows.Controls;

namespace ZO.DMM.AppNF
{
    public partial class ModActionWindow : Window
    {
        public string SelectedStage { get; private set; }
        private readonly ModItem _modItem;
        private readonly List<string> _stages;

        public string ActionType { get; private set; }

        public ModActionWindow(ModItem modItem, string actionType)
        {
            InitializeComponent();
            _modItem = modItem;
            _stages = ModItem.DB.GetDeployableStages();
            ActionType = actionType;
            DataContext = this;

            SourceStageComboBox.ItemsSource = _stages;
            TargetStageComboBox.ItemsSource = _stages;
            SourceStageComboBox.SelectedItem = _modItem.DeployedStage;

            InitializeUI(actionType);
        }

        private void InitializeUI(string actionType)
        {
            switch (actionType)
            {
                case "Promote":
                    Title = "Promote Mod to new Stage";
                    ActionButton.Content = "Promote";
                    SetTwoBoxLayout();
                    break;
                case "Package":
                    Title = "Package Mod for Distribution";
                    ActionButton.Content = "Package";
                    SetOneBoxLayout();
                    break;
                case "Deploy":
                    Title = "Deploy Mod to ModManager";
                    ActionButton.Content = "Deploy";
                    SetOneBoxLayout();
                    break;
            }

            // Set the action message
            ActionMessageTextBlock.Text = $"Taking action on : {_modItem.ModName}";
        }

        private void SetOneBoxLayout()
        {
            SourceStageLabel.Visibility = Visibility.Collapsed;
            SourceStageComboBox.Visibility = Visibility.Collapsed;
            TargetStageLabel.SetValue(Grid.ColumnProperty, 0);
            TargetStageLabel.SetValue(Grid.ColumnSpanProperty, 2);
            TargetStageComboBox.ItemsSource = _modItem.AvailableStages;
            TargetStageComboBox.SetValue(Grid.ColumnProperty, 0);
            TargetStageComboBox.SetValue(Grid.ColumnSpanProperty, 2);
        }

        private void SetTwoBoxLayout()
        {
            SourceStageLabel.Visibility = Visibility.Visible;
            SourceStageComboBox.Visibility = Visibility.Visible;
            SourceStageComboBox.ItemsSource = _modItem.AvailableStages;
            SourceStageComboBox.SelectionChanged += SourceStageComboBox_SelectionChanged;
            TargetStageLabel.SetValue(Grid.ColumnProperty, 1);
            TargetStageLabel.SetValue(Grid.ColumnSpanProperty, 1);
            TargetStageComboBox.SetValue(Grid.ColumnProperty, 1);
            TargetStageComboBox.SetValue(Grid.ColumnSpanProperty, 1);
            UpdateTargetStageComboBox();
        }

        private void SourceStageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTargetStageComboBox();
        }

        private void UpdateTargetStageComboBox()
        {
            var selectedSourceStage = SourceStageComboBox.SelectedItem as string;
            var validStages = _stages.ToList();

            if (selectedSourceStage != null)
            {
                validStages = validStages.Where(stage => stage != selectedSourceStage).ToList();
            }

            TargetStageComboBox.ItemsSource = validStages;
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedStage = TargetStageComboBox.SelectedItem as string ?? string.Empty;
            SelectedStage = selectedStage;

            if (ActionButton.Content.ToString() == "Deploy")
            {
                // Handle deploy logic
                _ = ModStageManager.DeployStage(_modItem, selectedStage);
            }
            else if (ActionButton.Content.ToString() == "Package")
            {
                // Handle package logic
                ModStageManager.PackageMod(_modItem, selectedStage);
            }
            else
            {
                // Handle promote logic
                _ = ModStageManager.PromoteModStage(_modItem, SourceStageComboBox.SelectedItem as string, selectedStage);
            }
            DialogResult = true;
            _modItem.SaveMod();
            Close();
        }
    }
}




