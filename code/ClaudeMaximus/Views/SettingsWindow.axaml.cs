using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClaudeMaximus.ViewModels;

namespace ClaudeMaximus.Views;

/// <remarks>Created by Claude</remarks>
public partial class SettingsWindow : Window
{
	public SettingsWindow()
	{
		InitializeComponent();
	}

	private async void OnBrowseSessionRootClicked(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SettingsViewModel vm)
			return;

		var folders = await StorageProvider.OpenFolderPickerAsync(
			new Avalonia.Platform.Storage.FolderPickerOpenOptions
			{
				Title = "Select Session Files Directory",
				AllowMultiple = false,
			});

		if (folders.Count > 0)
			vm.SessionFilesRoot = folders[0].Path.LocalPath;
	}

	private void OnSaveClicked(object? sender, RoutedEventArgs e)
		=> Close();
}
