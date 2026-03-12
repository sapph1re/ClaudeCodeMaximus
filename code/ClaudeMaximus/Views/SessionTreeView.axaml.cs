using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClaudeMaximus.ViewModels;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class SessionTreeView : UserControl
{
	public SessionTreeView()
	{
		InitializeComponent();
	}

	private async void OnAddDirectoryClicked(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SessionTreeViewModel vm)
			return;

		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
			new Avalonia.Platform.Storage.FolderPickerOpenOptions
			{
				Title = "Select Working Directory",
				AllowMultiple = false,
			});

		if (folders.Count == 0)
			return;

		var path = folders[0].Path.LocalPath;
		vm.AddDirectory(path);
	}

	private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is not SessionTreeViewModel vm)
			return;

		if (sender is TreeView tree && tree.SelectedItem is SessionNodeViewModel session)
			vm.SelectedSession = session;
		else if (e.RemovedItems.Count > 0)
			vm.SelectedSession = null;
	}
}
