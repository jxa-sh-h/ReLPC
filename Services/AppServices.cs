namespace ReLPC.Services;

/// <summary>Process-wide singletons shared by login, signup, and window navigation.</summary>
public static class AppServices
{
    public static ISessionService Session { get; } = new SessionService();
    public static IDatabaseService Database { get; } = new LiteDBService();
    public static IRecentDatasetsService RecentDatasets { get; } = new RecentDatasetsService(Database);
    public static IWindowService Windows { get; } = new WindowService();
}
