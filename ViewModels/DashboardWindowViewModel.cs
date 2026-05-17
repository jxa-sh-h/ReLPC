using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReLPC.Models;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public sealed class DashboardWindowViewModel : ViewModelBase
{
    private readonly IRecentDatasetsService _recentDatasets;
    private readonly IDatabaseService _database;
    private readonly ISessionService _session;

    public DashboardWindowViewModel(
        IRecentDatasetsService recentDatasets,
        IDatabaseService database,
        ISessionService session)
    {
        _recentDatasets = recentDatasets;
        _database = database;
        _session = session;
    }

    public ObservableCollection<DashboardDatasetItem> FeaturedDatasets { get; } = [];
    public ObservableCollection<DashboardDatasetItem> MoreDatasets { get; } = [];

    public string WelcomeName =>
        string.IsNullOrWhiteSpace(_session.CurrentUser?.Username)
            ? "USER"
            : _session.CurrentUser.Username.ToUpperInvariant();

    public string OwnerCaption =>
        string.IsNullOrWhiteSpace(_session.CurrentUser?.Username)
            ? "User >>"
            : $"{_session.CurrentUser.Username} >>";

    public bool HasFeaturedDatasets => FeaturedDatasets.Count > 0;
    public bool HasMoreDatasets => MoreDatasets.Count > 0;
    public bool HasRecentDatasets => HasFeaturedDatasets || HasMoreDatasets;

    public void RefreshRecentDatasets()
    {
        var userId = _session.CurrentUser?.Id ?? 0;
        var ownerCaption = OwnerCaption;
        var ordered = BuildOrderedDatasetList(userId);

        FeaturedDatasets.Clear();
        MoreDatasets.Clear();

        foreach (var dataset in ordered.Take(3))
        {
            FeaturedDatasets.Add(CreateItem(dataset, ownerCaption));
        }

        foreach (var dataset in ordered.Skip(3))
        {
            MoreDatasets.Add(CreateItem(dataset, ownerCaption));
        }

        OnPropertyChanged(nameof(HasFeaturedDatasets));
        OnPropertyChanged(nameof(HasMoreDatasets));
        OnPropertyChanged(nameof(HasRecentDatasets));
        OnPropertyChanged(nameof(WelcomeName));
        OnPropertyChanged(nameof(OwnerCaption));
    }

    private List<DatasetRecord> BuildOrderedDatasetList(int userId)
    {
        var recent = _recentDatasets.GetRecentDatasets(userId);
        var allFromDb = _database.GetDatasets(userId);
        var ordered = new List<DatasetRecord>();
        var seen = new HashSet<int>();

        foreach (var dataset in recent)
        {
            if (seen.Add(dataset.Id))
            {
                ordered.Add(dataset);
            }
        }

        foreach (var dataset in allFromDb)
        {
            if (seen.Add(dataset.Id))
            {
                ordered.Add(dataset);
            }
        }

        return ordered.Take(20).ToList();
    }

    private static DashboardDatasetItem CreateItem(DatasetRecord dataset, string ownerCaption) =>
        new()
        {
            Dataset = dataset,
            OwnerCaption = ownerCaption
        };
}
