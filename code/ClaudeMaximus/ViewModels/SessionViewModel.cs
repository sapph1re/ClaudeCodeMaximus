using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading;
using Avalonia.Threading;
using ClaudeMaximus.Models;
using ClaudeMaximus.Services;
using ReactiveUI;

namespace ClaudeMaximus.ViewModels;

/// <remarks>Created by Claude</remarks>
public sealed class SessionViewModel : ViewModelBase
{
	private readonly SessionNodeViewModel _node;
	private readonly ISessionFileService _fileService;
	private readonly IClaudeProcessManager _processManager;
	private readonly IAppSettingsService _appSettings;
	private string _name;
	private string _inputText = string.Empty;
	private bool _isBusy;
	private CancellationTokenSource? _cts;

	public string Name
	{
		get => _name;
		private set => this.RaiseAndSetIfChanged(ref _name, value);
	}

	public string InputText
	{
		get => _inputText;
		set => this.RaiseAndSetIfChanged(ref _inputText, value);
	}

	public bool IsBusy
	{
		get => _isBusy;
		private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
	}

	public ObservableCollection<MessageEntryViewModel> Messages { get; } = [];

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	public SessionViewModel(
		SessionNodeViewModel node,
		ISessionFileService fileService,
		IClaudeProcessManager processManager,
		IAppSettingsService appSettings)
	{
		_node         = node;
		_fileService  = fileService;
		_processManager = processManager;
		_appSettings  = appSettings;
		_name         = node.Name;

		node.WhenAnyValue(x => x.Name).Subscribe(n => Name = n);

		var canSend = this.WhenAnyValue(x => x.IsBusy, busy => !busy);
		SendCommand = ReactiveCommand.CreateFromTask(SendAsync, canSend);
	}

	public void LoadFromFile()
	{
		var entries = _fileService.ReadEntries(_node.FileName);
		foreach (var entry in entries)
			Messages.Add(EntryToViewModel(entry));
	}

	private async System.Threading.Tasks.Task SendAsync()
	{
		var message = InputText.Trim();
		if (string.IsNullOrEmpty(message))
			return;

		InputText = string.Empty;
		SetBusy(true);

		_fileService.AppendMessage(_node.FileName, Constants.SessionFile.RoleUser, message);
		Messages.Add(new MessageEntryViewModel
		{
			Role      = Constants.SessionFile.RoleUser,
			Content   = message,
			Timestamp = DateTimeOffset.UtcNow,
		});

		_cts = new CancellationTokenSource();

		try
		{
			await _processManager.SendMessageAsync(
				workingDirectory: _node.Model.WorkingDirectory,
				claudePath:       _appSettings.Settings.ClaudePath,
				sessionId:        _node.Model.ClaudeSessionId,
				userMessage:      message,
				onEvent:          HandleStreamEvent,
				cancellationToken: _cts.Token);
		}
		finally
		{
			_cts.Dispose();
			_cts = null;
			SetBusy(false);
		}
	}

	private void HandleStreamEvent(ClaudeStreamEvent evt)
	{
		// File writes happen on the background thread (safe — append + flush)
		switch (evt.Type)
		{
			case "assistant" when evt.Content is not null:
				_fileService.AppendMessage(_node.FileName, Constants.SessionFile.RoleAssistant, evt.Content);
				break;
			case "system" when evt.Subtype is "compact":
				_fileService.AppendCompactionSeparator(_node.FileName);
				break;
			case "system" when evt.IsError && evt.Content is not null:
				_fileService.AppendMessage(_node.FileName, Constants.SessionFile.RoleSystem, evt.Content);
				break;
			case "result" when evt.SessionId is not null:
				_node.Model.ClaudeSessionId = evt.SessionId;
				_appSettings.Save();
				break;
		}

		// UI updates must be on the UI thread
		Dispatcher.UIThread.Post(() =>
		{
			switch (evt.Type)
			{
				case "assistant" when evt.Content is not null:
					Messages.Add(new MessageEntryViewModel
					{
						Role      = Constants.SessionFile.RoleAssistant,
						Content   = evt.Content,
						Timestamp = evt.Timestamp,
					});
					break;

				case "system" when evt.Subtype is "compact":
					Messages.Add(new MessageEntryViewModel
					{
						Role      = Constants.SessionFile.RoleCompaction,
						Content   = string.Empty,
						Timestamp = evt.Timestamp,
					});
					break;

				case "system" when evt.IsError && evt.Content is not null:
					Messages.Add(new MessageEntryViewModel
					{
						Role      = Constants.SessionFile.RoleSystem,
						Content   = evt.Content,
						Timestamp = evt.Timestamp,
					});
					break;
			}
		});
	}

	private void SetBusy(bool busy)
	{
		Dispatcher.UIThread.Post(() =>
		{
			IsBusy           = busy;
			_node.IsRunning  = busy;
		});
	}

	private static MessageEntryViewModel EntryToViewModel(SessionEntryModel entry)
		=> new()
		{
			Role      = entry.Role,
			Content   = entry.Content,
			Timestamp = entry.Timestamp,
		};
}
