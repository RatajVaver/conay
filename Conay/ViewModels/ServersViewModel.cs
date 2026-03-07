using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.ViewModels.Parts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Conay.ViewModels;

public enum TagFilterState
{
    None,
    Include,
    Exclude
}

public partial class ServersViewModel : PageViewModel
{
    private readonly ServerPresetFactory _presetFactory;
    private readonly ServerList _serverList;
    private readonly List<ServerPresetViewModel> _allPresets = [];

    public bool IsActuallyEmpty => _allPresets.Count == 0;
    public bool HasNoFilterResults => FilteredPresets.Count == 0 && _allPresets.Count > 0;

    [ObservableProperty]
    private bool _isLoadingForFilter;

    public ObservableCollection<ServerPresetViewModel> FilteredPresets { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModdedFilterIsInclude))]
    [NotifyPropertyChangedFor(nameof(ModdedFilterIsExclude))]
    private TagFilterState _moddedFilter = TagFilterState.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleplayFilterIsInclude))]
    [NotifyPropertyChangedFor(nameof(RoleplayFilterIsExclude))]
    private TagFilterState _roleplayFilter = TagFilterState.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MechPvpFilterIsInclude))]
    [NotifyPropertyChangedFor(nameof(MechPvpFilterIsExclude))]
    private TagFilterState _mechPvpFilter = TagFilterState.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DicePvpFilterIsInclude))]
    [NotifyPropertyChangedFor(nameof(DicePvpFilterIsExclude))]
    private TagFilterState _dicePvpFilter = TagFilterState.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EroticFilterIsInclude))]
    [NotifyPropertyChangedFor(nameof(EroticFilterIsExclude))]
    private TagFilterState _eroticFilter = TagFilterState.None;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterModeLabel))]
    private bool _filterModeAnd = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinPlayerCountLabel))]
    private double _minPlayerCount;

    public bool ModdedFilterIsInclude => ModdedFilter == TagFilterState.Include;
    public bool ModdedFilterIsExclude => ModdedFilter == TagFilterState.Exclude;
    public bool RoleplayFilterIsInclude => RoleplayFilter == TagFilterState.Include;
    public bool RoleplayFilterIsExclude => RoleplayFilter == TagFilterState.Exclude;
    public bool MechPvpFilterIsInclude => MechPvpFilter == TagFilterState.Include;
    public bool MechPvpFilterIsExclude => MechPvpFilter == TagFilterState.Exclude;
    public bool DicePvpFilterIsInclude => DicePvpFilter == TagFilterState.Include;
    public bool DicePvpFilterIsExclude => DicePvpFilter == TagFilterState.Exclude;
    public bool EroticFilterIsInclude => EroticFilter == TagFilterState.Include;
    public bool EroticFilterIsExclude => EroticFilter == TagFilterState.Exclude;

    public string FilterModeLabel => FilterModeAnd ? "AND" : "OR";
    public string MinPlayerCountLabel => MinPlayerCount < 1 ? "Any" : $"{(int)MinPlayerCount}+";

    public ServersViewModel(ServerPresetFactory presetFactory, ServerList serverList)
    {
        _presetFactory = presetFactory;
        _serverList = serverList;

        FilteredPresets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoFilterResults));

        _ = LoadRemoteServers();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterModeAndChanged(bool value) => _ = ApplyFiltersWithLoad();
    partial void OnModdedFilterChanged(TagFilterState value) => _ = ApplyFiltersWithLoad();
    partial void OnRoleplayFilterChanged(TagFilterState value) => _ = ApplyFiltersWithLoad();
    partial void OnMechPvpFilterChanged(TagFilterState value) => _ = ApplyFiltersWithLoad();
    partial void OnDicePvpFilterChanged(TagFilterState value) => _ = ApplyFiltersWithLoad();
    partial void OnEroticFilterChanged(TagFilterState value) => _ = ApplyFiltersWithLoad();
    partial void OnMinPlayerCountChanged(double value) => _ = ApplyFiltersWithLoad();

    private bool _filterLoadInProgress;

    private async Task ApplyFiltersWithLoad()
    {
        if (!AnyDataFilterActive)
        {
            ApplyFilters();
            return;
        }

        if (_filterLoadInProgress)
            return;

        List<ServerPresetViewModel> unloaded = _allPresets.Where(p => !p.IsDataLoaded).ToList();
        if (unloaded.Count > 0)
        {
            _filterLoadInProgress = true;
            IsLoadingForFilter = true;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            await Task.WhenAll(unloaded.Select(async p =>
            {
                await p.LoadDataAsync();
                ApplyFilters();
            }));

            IsLoadingForFilter = false;
            _filterLoadInProgress = false;
        }

        ApplyFilters();
    }

    [RelayCommand]
    private void ToggleModdedFilter() => ModdedFilter = Cycle(ModdedFilter);

    [RelayCommand]
    private void ToggleRoleplayFilter() => RoleplayFilter = Cycle(RoleplayFilter);

    [RelayCommand]
    private void ToggleMechPvpFilter() => MechPvpFilter = Cycle(MechPvpFilter);

    [RelayCommand]
    private void ToggleDicePvpFilter() => DicePvpFilter = Cycle(DicePvpFilter);

    [RelayCommand]
    private void ToggleEroticFilter() => EroticFilter = Cycle(EroticFilter);

    [RelayCommand]
    private void ToggleFilterMode() => FilterModeAnd = !FilterModeAnd;

    private static TagFilterState Cycle(TagFilterState current) => current switch
    {
        TagFilterState.None => TagFilterState.Include,
        TagFilterState.Include => TagFilterState.Exclude,
        _ => TagFilterState.None
    };

    private CancellationTokenSource? _preloadCancel;

    private async Task LoadRemoteServers()
    {
        RefreshServers();

        while (!_serverList.RemoteServersLoaded)
        {
            await Task.Delay(50);
            RefreshServers();
        }

        _preloadCancel?.Cancel();
        _preloadCancel = new CancellationTokenSource();
        _ = PreloadServerDataAsync(_preloadCancel.Token);
    }

    private async Task PreloadServerDataAsync(CancellationToken token)
    {
        using SemaphoreSlim sem = new(6);
        List<Task> tasks = _allPresets.Select(async p =>
        {
            await sem.WaitAsync(token);
            try
            {
                await p.LoadDataAsync();
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RefreshServers()
    {
        foreach (ServerPresetViewModel preset in _allPresets)
            preset.PropertyChanged -= OnPresetPropertyChanged;

        _allPresets.Clear();

        List<ServerInfo> servers = _serverList.GetRemoteServers();
        foreach (ServerInfo server in servers)
        {
            ServerPresetViewModel preset = _presetFactory.Create(server);
            preset.PropertyChanged += OnPresetPropertyChanged;
            _allPresets.Add(preset);
        }

        OnPropertyChanged(nameof(IsActuallyEmpty));
        ApplyFilters();
    }

    private bool AnyDataFilterActive =>
        ModdedFilter != TagFilterState.None ||
        RoleplayFilter != TagFilterState.None ||
        MechPvpFilter != TagFilterState.None ||
        DicePvpFilter != TagFilterState.None ||
        EroticFilter != TagFilterState.None ||
        MinPlayerCount >= 1;

    private CancellationTokenSource? _filterDebounce;

    private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ServerPresetViewModel.IsModded)
            or nameof(ServerPresetViewModel.IsRoleplay)
            or nameof(ServerPresetViewModel.IsMechPvP)
            or nameof(ServerPresetViewModel.IsDicePvP)
            or nameof(ServerPresetViewModel.IsErotic)
            or nameof(ServerPresetViewModel.Players))) return;
        if (AnyDataFilterActive)
            ScheduleFilterDebounce();
    }

    private void ScheduleFilterDebounce()
    {
        _filterDebounce?.Cancel();
        _filterDebounce = new CancellationTokenSource();
        _ = DebounceFilter(_filterDebounce.Token);
    }

    private async Task DebounceFilter(CancellationToken token)
    {
        try
        {
            await Task.Delay(80, token);
            ApplyFilters();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyFilters()
    {
        List<ServerPresetViewModel> desired = _allPresets.Where(MatchesFilter).ToList();

        if (desired.Count == FilteredPresets.Count && desired.SequenceEqual(FilteredPresets))
            return;

        for (int i = FilteredPresets.Count - 1; i >= 0; i--)
            if (!desired.Contains(FilteredPresets[i]))
                FilteredPresets.RemoveAt(i);

        for (int i = 0; i < desired.Count; i++)
        {
            if (i < FilteredPresets.Count && FilteredPresets[i] == desired[i])
                continue;

            int existing = FilteredPresets.IndexOf(desired[i]);
            if (existing >= 0)
                FilteredPresets.Move(existing, i);
            else
                FilteredPresets.Insert(i, desired[i]);
        }
    }

    private bool MatchesFilter(ServerPresetViewModel preset)
    {
        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !preset.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        if (MinPlayerCount >= 1 && ParsePlayerCount(preset.Players) < (int)MinPlayerCount)
            return false;

        (bool hasTag, TagFilterState filter)[] tagChecks =
        [
            (preset.IsModded, ModdedFilter),
            (preset.IsRoleplay, RoleplayFilter),
            (preset.IsMechPvP, MechPvpFilter),
            (preset.IsDicePvP, DicePvpFilter),
            (preset.IsErotic, EroticFilter),
        ];

        if (tagChecks.Any(t => t is { filter: TagFilterState.Exclude, hasTag: true }))
            return false;

        (bool hasTag, TagFilterState filter)[] includes =
            tagChecks.Where(t => t.filter == TagFilterState.Include).ToArray();
        if (includes.Length == 0)
            return true;

        return FilterModeAnd
            ? includes.All(t => t.hasTag)
            : includes.Any(t => t.hasTag);
    }

    private static int ParsePlayerCount(string players)
    {
        if (string.IsNullOrEmpty(players)) return 0;
        string first = players.TrimStart('~').Trim().Split('/')[0].Trim();
        return int.TryParse(first, out int count) ? count : 0;
    }
}