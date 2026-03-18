using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClaudeMaximus.Models;
using ClaudeMaximus.Services;
using ReactiveUI;
using Serilog;

namespace ClaudeMaximus.ViewModels;

/// <summary>
/// ViewModel for the session import picker dialog.
/// Manages discovery, title generation, search, and selection.
/// </summary>
/// <remarks>Created by Claude</remarks>
public sealed class ImportPickerViewModel : ViewModelBase
{
	private static readonly ILogger _log = Log.ForContext<ImportPickerViewModel>();

	private readonly IClaudeSessionImportService _importService;
	private readonly IClaudeAssistService _assistService;
	private string _searchText = string.Empty;
	private bool _isSearching;
	private bool _isGeneratingTitles;
	private string _statusMessage = string.Empty;
	private string _titleProgressText = string.Empty;
	private double _titleProgressValue;
	private double _titleProgressMax = 1;
	private CancellationTokenSource? _titleCts;

	/// <summary>All discovered session items (master list).</summary>
	private List<ImportSessionItemViewModel> _allItems = [];

	/// <summary>Currently visible items (may be filtered by search).</summary>
	public ObservableCollection<ImportSessionItemViewModel> Items { get; } = [];

	public string SearchText
	{
		get => _searchText;
		set => this.RaiseAndSetIfChanged(ref _searchText, value);
	}

	public bool IsSearching
	{
		get => _isSearching;
		set => this.RaiseAndSetIfChanged(ref _isSearching, value);
	}

	public bool IsGeneratingTitles
	{
		get => _isGeneratingTitles;
		set => this.RaiseAndSetIfChanged(ref _isGeneratingTitles, value);
	}

	public string StatusMessage
	{
		get => _statusMessage;
		set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
	}

	public string TitleProgressText
	{
		get => _titleProgressText;
		set => this.RaiseAndSetIfChanged(ref _titleProgressText, value);
	}

	public double TitleProgressValue
	{
		get => _titleProgressValue;
		set => this.RaiseAndSetIfChanged(ref _titleProgressValue, value);
	}

	public double TitleProgressMax
	{
		get => _titleProgressMax;
		set => this.RaiseAndSetIfChanged(ref _titleProgressMax, value);
	}

	public bool HasItems => Items.Count > 0;

	public bool HasNoItems => Items.Count == 0 && !IsSearching;

	/// <summary>Returns the selected (checked) items that are importable.</summary>
	public IReadOnlyList<ImportSessionItemViewModel> SelectedItems =>
		_allItems.Where(i => i.IsSelected).ToList();

	public bool HasSelection => _allItems.Any(i => i.IsSelected);

	public ImportPickerViewModel(
		IClaudeSessionImportService importService,
		IClaudeAssistService assistService)
	{
		_importService = importService;
		_assistService = assistService;
	}

	/// <summary>
	/// Discovers sessions for the given working directory and populates the picker.
	/// </summary>
	public void DiscoverSessions(string workingDirectory, IReadOnlySet<string> alreadyImportedIds)
	{
		var summaries = _importService.DiscoverSessions(workingDirectory);

		_allItems = summaries.Select(s =>
			new ImportSessionItemViewModel(s, alreadyImportedIds.Contains(s.SessionId))
		).ToList();

		Items.Clear();
		foreach (var item in _allItems)
			Items.Add(item);

		this.RaisePropertyChanged(nameof(HasItems));
		this.RaisePropertyChanged(nameof(HasNoItems));

		if (_allItems.Count == 0)
			StatusMessage = "No Claude Code sessions found for this project directory.";
		else
			StatusMessage = $"Found {_allItems.Count} sessions.";

		// Start async title generation
		_ = GenerateTitlesAsync();
	}

	/// <summary>
	/// Performs a search: tries Claude-powered semantic search first,
	/// falls back to local substring matching.
	/// </summary>
	public async Task SearchAsync()
	{
		var query = SearchText?.Trim();

		if (string.IsNullOrEmpty(query))
		{
			// Clear search — show all items in original order
			Items.Clear();
			foreach (var item in _allItems)
				Items.Add(item);
			StatusMessage = $"Found {_allItems.Count} sessions.";
			this.RaisePropertyChanged(nameof(HasItems));
			this.RaisePropertyChanged(nameof(HasNoItems));
			return;
		}

		IsSearching = true;
		StatusMessage = "Searching...";

		try
		{
			var summaries = _allItems.Select(i => i.Summary).ToList();
			var matchedIds = await _assistService.SearchSessionsAsync(summaries, query);

			if (matchedIds.Count > 0)
			{
				// Claude-powered search succeeded: reorder by relevance
				ReorderByIds(matchedIds);
				StatusMessage = $"Found {matchedIds.Count} matching sessions.";
			}
			else
			{
				// Fallback: local substring match
				FallbackSubstringSearch(query);
			}
		}
		catch (Exception ex)
		{
			_log.Warning(ex, "SearchAsync: Claude search failed, using fallback");
			FallbackSubstringSearch(query);
		}
		finally
		{
			IsSearching = false;
			this.RaisePropertyChanged(nameof(HasItems));
			this.RaisePropertyChanged(nameof(HasNoItems));
		}
	}

	/// <summary>
	/// Cancels any running title generation.
	/// </summary>
	public void CancelTitleGeneration()
	{
		_titleCts?.Cancel();
	}

	private async Task GenerateTitlesAsync()
	{
		_titleCts?.Cancel();
		_titleCts = new CancellationTokenSource();
		var ct = _titleCts.Token;

		var pendingItems = _allItems
			.Where(i => i.Summary.FirstUserPrompt != null)
			.ToList();
		var summaries = pendingItems.Select(i => i.Summary).ToList();

		if (summaries.Count == 0)
			return;

		IsGeneratingTitles = true;
		TitleProgressMax = summaries.Count;
		TitleProgressValue = 0;
		TitleProgressText = $"Generating titles... 0/{summaries.Count}";

		try
		{
			await _assistService.GenerateTitlesAsync(summaries, OnBatchComplete, ct);

			if (ct.IsCancellationRequested)
				return;

			// Mark any remaining items as no longer pending (title generation finished or failed)
			foreach (var item in pendingItems)
			{
				if (item.IsTitlePending)
					item.IsTitlePending = false;
			}
		}
		catch (OperationCanceledException)
		{
			// Expected when cancelled
		}
		catch (Exception ex)
		{
			_log.Warning(ex, "GenerateTitlesAsync: title generation failed");
		}
		finally
		{
			IsGeneratingTitles = false;
			TitleProgressText = string.Empty;
		}
	}

	private void OnBatchComplete(Dictionary<string, string> allTitlesSoFar)
	{
		Avalonia.Threading.Dispatcher.UIThread.Post(() =>
		{
			foreach (var (sessionId, title) in allTitlesSoFar)
			{
				var item = _allItems.FirstOrDefault(i => i.SessionId == sessionId);
				if (item != null && item.IsTitlePending)
					item.UpdateTitle(title);
			}

			TitleProgressValue = allTitlesSoFar.Count;
			var total = (int)TitleProgressMax;
			TitleProgressText = $"Generating titles... {allTitlesSoFar.Count}/{total}";
		});
	}

	private void ReorderByIds(List<string> orderedIds)
	{
		// Build a lookup of local items by session ID for validation
		var itemLookup = new Dictionary<string, ImportSessionItemViewModel>();
		foreach (var item in _allItems)
			itemLookup[item.SessionId] = item;

		var matched = new List<ImportSessionItemViewModel>();
		var matchedSet = new HashSet<string>();

		// Add matched items in relevance order, skipping duplicates and unknown IDs
		foreach (var id in orderedIds)
		{
			if (matchedSet.Contains(id))
				continue;
			if (!itemLookup.TryGetValue(id, out var item))
				continue;
			matched.Add(item);
			matchedSet.Add(id);
		}

		// If no valid matches found, fall back (don't show a misleading "found N" message)
		if (matched.Count == 0)
		{
			StatusMessage = "No matching sessions found (search returned no valid results).";
			return;
		}

		// Add remaining items after matched
		Items.Clear();
		foreach (var item in matched)
			Items.Add(item);
		foreach (var item in _allItems)
		{
			if (!matchedSet.Contains(item.SessionId))
				Items.Add(item);
		}
	}

	private void FallbackSubstringSearch(string query)
	{
		var lowerQuery = query.ToLowerInvariant();
		var matched = new List<ImportSessionItemViewModel>();
		var unmatched = new List<ImportSessionItemViewModel>();

		foreach (var item in _allItems)
		{
			var title = (item.DisplayTitle ?? string.Empty).ToLowerInvariant();
			var prompt = (item.Summary.FirstUserPrompt ?? string.Empty).ToLowerInvariant();

			if (title.Contains(lowerQuery) || prompt.Contains(lowerQuery))
				matched.Add(item);
			else
				unmatched.Add(item);
		}

		Items.Clear();
		foreach (var item in matched)
			Items.Add(item);
		foreach (var item in unmatched)
			Items.Add(item);

		StatusMessage = matched.Count > 0
			? $"Found {matched.Count} matching sessions (local search)."
			: "No matching sessions found.";
	}
}
