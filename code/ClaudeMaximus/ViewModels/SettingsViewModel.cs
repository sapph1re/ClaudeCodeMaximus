using System.Reactive;
using ClaudeMaximus.Services;
using ReactiveUI;

namespace ClaudeMaximus.ViewModels;

/// <remarks>Created by Claude</remarks>
public sealed class SettingsViewModel : ViewModelBase
{
	private readonly IAppSettingsService _appSettings;
	private string _sessionFilesRoot;
	private string _claudePath;

	public string SessionFilesRoot
	{
		get => _sessionFilesRoot;
		set => this.RaiseAndSetIfChanged(ref _sessionFilesRoot, value);
	}

	public string ClaudePath
	{
		get => _claudePath;
		set => this.RaiseAndSetIfChanged(ref _claudePath, value);
	}

	public ReactiveCommand<Unit, Unit> SaveCommand { get; }

	public SettingsViewModel(IAppSettingsService appSettings)
	{
		_appSettings = appSettings;
		_sessionFilesRoot = appSettings.Settings.SessionFilesRoot;
		_claudePath = appSettings.Settings.ClaudePath;

		SaveCommand = ReactiveCommand.Create(Save);
	}

	private void Save()
	{
		_appSettings.Settings.SessionFilesRoot = _sessionFilesRoot;
		_appSettings.Settings.ClaudePath = _claudePath;
		_appSettings.Save();
	}
}
