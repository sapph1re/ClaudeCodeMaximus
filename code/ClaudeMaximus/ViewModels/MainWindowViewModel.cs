using System.Reactive;
using ClaudeMaximus.Services;
using ReactiveUI;

namespace ClaudeMaximus.ViewModels;

/// <remarks>Created by Claude</remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
	private readonly IAppSettingsService _appSettings;
	private double _splitterPosition;

	public SessionTreeViewModel SessionTree { get; }

	public double SplitterPosition
	{
		get => _splitterPosition;
		set
		{
			this.RaiseAndSetIfChanged(ref _splitterPosition, value);
			_appSettings.Settings.Window.SplitterPosition = value;
		}
	}

	public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public MainWindowViewModel(
		IAppSettingsService appSettings,
		SessionTreeViewModel sessionTree)
	{
		_appSettings = appSettings;
		SessionTree = sessionTree;
		_splitterPosition = appSettings.Settings.Window.SplitterPosition;

		OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
		ExitCommand = ReactiveCommand.Create(Exit);
	}

	private void OpenSettings()
	{
		var vm = new SettingsViewModel(_appSettings);
		var window = new Views.SettingsWindow { DataContext = vm };
		window.Show();
	}

	private static void Exit()
	{
		if (Avalonia.Application.Current?.ApplicationLifetime is
			Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt)
			lt.Shutdown();
	}
}
