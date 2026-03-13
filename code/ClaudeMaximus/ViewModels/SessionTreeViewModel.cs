using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
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
	private readonly IClaudeSessionStatusService _claudeSessionStatus;
	private readonly ISessionSearchService _searchService;
	private string _searchText = string.Empty;
	private SessionNodeViewModel? _selectedSession;
	private CancellationTokenSource? _searchCts;

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
		ISessionFileService sessionFileService,
		IClaudeSessionStatusService claudeSessionStatus,
		ISessionSearchService searchService)
	{
		_appSettings = appSettings;
		_labelService = labelService;
		_sessionFileService = sessionFileService;
		_claudeSessionStatus = claudeSessionStatus;
		_searchService = searchService;

		AddDirectoryCommand = ReactiveCommand.Create(PromptAddDirectory);

		LoadFromSettings();
		RefreshSessionResumability();
		RefreshLastPromptTimes();

		this.WhenAnyValue(x => x.SearchText)
			.Throttle(TimeSpan.FromMilliseconds(300))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => ApplySearchFilter());

		var timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(Constants.ClaudeSessions.StatusCheckIntervalSeconds),
		};
		timer.Tick += (_, _) => RefreshSessionResumability();
		timer.Start();
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
		var model = new GroupNodeModel { Name = name, WorkingDirectory = parent.Path };
		var vm = new GroupNodeViewModel(model);
		parent.AddGroup(vm);
		_appSettings.Save();
		return vm;
	}

	public GroupNodeViewModel AddGroupToGroup(GroupNodeViewModel parent, string name)
	{
		var model = new GroupNodeModel { Name = name, WorkingDirectory = parent.WorkingDirectory };
		var vm = new GroupNodeViewModel(model);
		parent.AddGroup(vm);
		_appSettings.Save();
		return vm;
	}

	public SessionNodeViewModel AddSession(DirectoryNodeViewModel parent, string name)
	{
		var fileName = _sessionFileService.CreateSessionFile();
		var model = new SessionNodeModel { Name = name, FileName = fileName, WorkingDirectory = parent.Path };
		var vm = new SessionNodeViewModel(model);
		parent.AddSession(vm);
		_appSettings.Save();
		return vm;
	}

	public SessionNodeViewModel AddSessionToGroup(GroupNodeViewModel parent, string name)
	{
		var fileName = _sessionFileService.CreateSessionFile();
		var model = new SessionNodeModel { Name = name, FileName = fileName, WorkingDirectory = parent.WorkingDirectory };
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

	private void RefreshSessionResumability()
	{
		foreach (var dir in Directories)
			RefreshChildren(dir.Children);
	}

	private void RefreshChildren(ObservableCollection<ViewModelBase> children)
	{
		foreach (var child in children)
		{
			switch (child)
			{
				case SessionNodeViewModel session:
					session.IsResumable = session.Model.ClaudeSessionId is not null
						&& _claudeSessionStatus.IsSessionResumable(
							session.Model.WorkingDirectory,
							session.Model.ClaudeSessionId);
					break;
				case GroupNodeViewModel group:
					RefreshChildren(group.Children);
					break;
			}
		}
	}

	// --- Search ---

	private void ApplySearchFilter()
	{
		_searchCts?.Cancel();

		var query = SearchText?.Trim() ?? string.Empty;

		if (string.IsNullOrEmpty(query))
		{
			ShowAllNodes();
			return;
		}

		var cts = new CancellationTokenSource();
		_searchCts = cts;

		Task.Run(() =>
		{
			if (cts.Token.IsCancellationRequested)
				return;

			var matches = _searchService.FindMatchingSessionFiles(query);

			if (cts.Token.IsCancellationRequested)
				return;

			Dispatcher.UIThread.Post(() =>
			{
				if (cts.Token.IsCancellationRequested)
					return;

				foreach (var dir in Directories)
				{
					var dirHasMatch = ApplyFilterToChildren(dir.Children, matches);
					dir.IsVisible = dirHasMatch;
				}
			});
		}, cts.Token);
	}

	private static bool ApplyFilterToChildren(ObservableCollection<ViewModelBase> children, IReadOnlySet<string> matches)
	{
		var anyVisible = false;

		foreach (var child in children)
		{
			switch (child)
			{
				case SessionNodeViewModel session:
					var visible = matches.Contains(session.FileName);
					session.IsVisible = visible;
					if (visible) anyVisible = true;
					break;
				case GroupNodeViewModel group:
					var groupHasMatch = ApplyFilterToChildren(group.Children, matches);
					group.IsVisible = groupHasMatch;
					if (groupHasMatch) anyVisible = true;
					break;
			}
		}

		return anyVisible;
	}

	private void ShowAllNodes()
	{
		foreach (var dir in Directories)
		{
			dir.IsVisible = true;
			ShowAllChildren(dir.Children);
		}
	}

	private static void ShowAllChildren(ObservableCollection<ViewModelBase> children)
	{
		foreach (var child in children)
		{
			switch (child)
			{
				case SessionNodeViewModel session:
					session.IsVisible = true;
					break;
				case GroupNodeViewModel group:
					group.IsVisible = true;
					ShowAllChildren(group.Children);
					break;
			}
		}
	}

	// --- Last prompt time ---

	private void RefreshLastPromptTimes()
	{
		foreach (var dir in Directories)
			RefreshLastPromptTimesInChildren(dir.Children);
	}

	private void RefreshLastPromptTimesInChildren(ObservableCollection<ViewModelBase> children)
	{
		foreach (var child in children)
		{
			switch (child)
			{
				case SessionNodeViewModel session:
					LoadLastPromptTime(session);
					break;
				case GroupNodeViewModel group:
					RefreshLastPromptTimesInChildren(group.Children);
					break;
			}
		}
	}

	private void LoadLastPromptTime(SessionNodeViewModel session)
	{
		try
		{
			var entries = _sessionFileService.ReadEntries(session.FileName);
			for (var i = entries.Count - 1; i >= 0; i--)
			{
				if (entries[i].Role == Constants.SessionFile.RoleUser)
				{
					var local = entries[i].Timestamp.LocalDateTime;
					session.LastPromptTime = local.ToString("yyyy-MM-dd HH:mm");
					return;
				}
			}
		}
		catch
		{
			// Session file may not exist or be corrupted; leave LastPromptTime null.
		}

		session.LastPromptTime = null;
	}

}
