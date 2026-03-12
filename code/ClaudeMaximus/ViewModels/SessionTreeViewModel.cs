using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using ClaudeMaximus.Models;
using ClaudeMaximus.Services;
using ReactiveUI;

namespace ClaudeMaximus.ViewModels;

/// <remarks>Created by Claude</remarks>
public sealed class SessionTreeViewModel : ViewModelBase
{
	private readonly IAppSettingsService _appSettings;
	private readonly IDirectoryLabelService _labelService;
	private readonly ISessionFileService _sessionFileService;
	private string _searchText = string.Empty;
	private SessionNodeViewModel? _selectedSession;

	public ObservableCollection<DirectoryNodeViewModel> Directories { get; } = [];

	public string SearchText
	{
		get => _searchText;
		set => this.RaiseAndSetIfChanged(ref _searchText, value);
	}

	public SessionNodeViewModel? SelectedSession
	{
		get => _selectedSession;
		set => this.RaiseAndSetIfChanged(ref _selectedSession, value);
	}

	public ReactiveCommand<Unit, Unit> AddDirectoryCommand { get; }

	public SessionTreeViewModel(
		IAppSettingsService appSettings,
		IDirectoryLabelService labelService,
		ISessionFileService sessionFileService)
	{
		_appSettings = appSettings;
		_labelService = labelService;
		_sessionFileService = sessionFileService;

		AddDirectoryCommand = ReactiveCommand.Create(PromptAddDirectory);

		LoadFromSettings();
	}

	// --- Add operations ---

	public DirectoryNodeViewModel AddDirectory(string path)
	{
		var normalised = Path.GetFullPath(path);
		var existing = Directories.FirstOrDefault(d =>
			string.Equals(d.Path, normalised, StringComparison.OrdinalIgnoreCase));

		if (existing != null)
			return existing;

		var model = new DirectoryNodeModel { Path = normalised };
		var vm = new DirectoryNodeViewModel(model, _labelService);
		Directories.Add(vm);
		_appSettings.Settings.Tree.Add(model);
		_appSettings.Save();
		return vm;
	}

	public GroupNodeViewModel AddGroup(DirectoryNodeViewModel parent, string name)
	{
		var model = new GroupNodeModel { Name = name };
		var vm = new GroupNodeViewModel(model);
		parent.AddGroup(vm);
		_appSettings.Save();
		return vm;
	}

	public GroupNodeViewModel AddGroupToGroup(GroupNodeViewModel parent, string name)
	{
		var model = new GroupNodeModel { Name = name };
		var vm = new GroupNodeViewModel(model);
		parent.AddGroup(vm);
		_appSettings.Save();
		return vm;
	}

	public SessionNodeViewModel AddSession(DirectoryNodeViewModel parent, string name)
	{
		var fileName = _sessionFileService.CreateSessionFile();
		var model = new SessionNodeModel { Name = name, FileName = fileName };
		var vm = new SessionNodeViewModel(model);
		parent.AddSession(vm);
		_appSettings.Save();
		return vm;
	}

	public SessionNodeViewModel AddSessionToGroup(GroupNodeViewModel parent, string name)
	{
		var fileName = _sessionFileService.CreateSessionFile();
		var model = new SessionNodeModel { Name = name, FileName = fileName };
		var vm = new SessionNodeViewModel(model);
		parent.AddSession(vm);
		_appSettings.Save();
		return vm;
	}

	// --- Rename operations ---

	public void RenameGroup(GroupNodeViewModel group, string newName)
	{
		group.Name = newName;
		_appSettings.Save();
	}

	public void RenameSession(SessionNodeViewModel session, string newName)
	{
		session.Name = newName;
		_appSettings.Save();
	}

	// --- Delete operations ---

	public bool TryDeleteDirectory(DirectoryNodeViewModel directory)
	{
		if (!directory.CanDelete)
			return false;

		Directories.Remove(directory);
		_appSettings.Settings.Tree.Remove(directory.Model);
		_appSettings.Save();
		return true;
	}

	public bool TryDeleteGroup(DirectoryNodeViewModel parent, GroupNodeViewModel group)
	{
		if (!group.CanDelete)
			return false;

		parent.Children.Remove(group);
		parent.Model.Groups.Remove(group.Model);
		_appSettings.Save();
		return true;
	}

	public bool TryDeleteGroupFromGroup(GroupNodeViewModel parent, GroupNodeViewModel group)
	{
		if (!group.CanDelete)
			return false;

		parent.Children.Remove(group);
		parent.Model.Groups.Remove(group.Model);
		_appSettings.Save();
		return true;
	}

	public bool TryDeleteSession(DirectoryNodeViewModel parent, SessionNodeViewModel session)
	{
		if (_sessionFileService.SessionFileExists(session.FileName))
			return false;

		parent.Children.Remove(session);
		parent.Model.Sessions.Remove(session.Model);
		_appSettings.Save();
		return true;
	}

	public bool TryDeleteSessionFromGroup(GroupNodeViewModel parent, SessionNodeViewModel session)
	{
		if (_sessionFileService.SessionFileExists(session.FileName))
			return false;

		parent.Children.Remove(session);
		parent.Model.Sessions.Remove(session.Model);
		_appSettings.Save();
		return true;
	}

	// --- Private helpers ---

	private void LoadFromSettings()
	{
		foreach (var dirModel in _appSettings.Settings.Tree)
			Directories.Add(new DirectoryNodeViewModel(dirModel, _labelService));
	}

	private void PromptAddDirectory()
	{
		// Folder picker is invoked from the view's code-behind (requires Window reference).
		// This command signals intent; the view handles the dialog.
	}
}
