using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace DMM.Avalonia;

internal static class DependencyReviewCoordinator
{
    public static async Task<IReadOnlyDictionary<string, HashSet<string>>?> BuildSelectionsAsync(
        Window owner,
        MainWindowViewModel viewModel,
        string? gameFolder,
        IReadOnlyList<GameFolderStageSelection> selections)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        var orderedSelections = selections.OrderBy(x => x.PluginName, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < orderedSelections.Count; index++)
        {
            var selection = orderedSelections[index];
            viewModel.StatusMessage =
                $"Dependency review {index + 1}/{orderedSelections.Count}: {selection.ModName}. Mark good files to keep, then click Continue.";

            if (!viewModel.TryCollectDependencyPreview(gameFolder, selection, out var preview, out var error))
            {
                viewModel.StatusMessage = $"Dependency preview failed for {selection.ModName}: {error}";
                continue;
            }

            var reviewEntries = preview.Discovery.Entries
                .Where(x => !MainWindowViewModel.IsPluginOrArchiveRelativePath(x.RelativeDataPath))
                .ToList();

            var dialog = new DependencyReviewWindow(
                selection.ModName,
                selection.PluginName,
                preview.PluginFiles,
                preview.Ba2Files,
                reviewEntries,
                preview.Discovery.MissingReferences,
                preview.Discovery.UndefinedDiscard,
                preview.Discovery.ParentArchiveReferences,
                preview.ScanRoot,
                preview.GameRoot,
                preview.Discovery.ArchiveHitKinds);

            var decision = await dialog.ShowDialog<DependencyReviewDecision?>(owner);
            if (decision is null)
            {
                return null;
            }

            map[MainWindowViewModel.BuildSelectionReviewKey(selection)] = new HashSet<string>(decision.KeepRelativePaths, StringComparer.OrdinalIgnoreCase);
            viewModel.StatusMessage =
                $"Saved dependency selection for {selection.ModName} ({decision.KeepRelativePaths.Count} file(s) kept).";
        }

        return map;
    }
}
