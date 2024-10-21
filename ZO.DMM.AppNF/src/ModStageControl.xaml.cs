using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace ZO.DMM.AppNF
{
    public partial class ModStageControl : UserControl
    {
        public ObservableCollection<string> SourceStages { get; set; }
        public ObservableCollection<string> DestinationStages { get; set; }

        public ModStageControl()
        {
            InitializeComponent();
            DataContext = this; // Set DataContext to the current instance
            LoadStages();
        }

        private void LoadStages()
        {
            try
            {
                // Fetch stages for SourceStages
                var stagesQuery = "SELECT StageName FROM Stages WHERE isReserved = 0";
                var stages = ExecuteStageQuery(stagesQuery);

                SourceStages = new ObservableCollection<string>(stages);

                // Fetch stages for DestinationStages
                var destinationStagesQuery = "SELECT StageName FROM Stages WHERE isReserved = 0 AND isSource = 0";
                var destinationStages = ExecuteStageQuery(destinationStagesQuery);

                DestinationStages = new ObservableCollection<string>(destinationStages);
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                _ = MessageBox.Show($"Failed to load stages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> ExecuteStageQuery(string query)
        {
            var stages = new List<string>();
            using (var connection = DbManager.Instance.GetConnection())
            {
                connection.Open();
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stages.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return stages;
        }

        private void PromoteButton_Click(object sender, RoutedEventArgs e)
        {
            var modItem = DataContext as ModItem;
            if (modItem != null)
            {
                var sourceStage = SourceStageComboBox.SelectedItem as string;
                var destinationStage = DestinationStageComboBox.SelectedItem as string;

                // Check if either stage is an empty string
                if (string.IsNullOrEmpty(sourceStage) || string.IsNullOrEmpty(destinationStage))
                {
                    _ = MessageBox.Show("Both source and destination stages must be selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _ = ModStageManager.PromoteModStage(modItem, sourceStage, destinationStage);
            }
        }
    }
}
