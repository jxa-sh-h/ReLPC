namespace ReLPC.Services;

/// <summary>Process-wide singletons shared by login, signup, and window navigation.</summary>
public static class AppServices
{
    public static ISessionService Session { get; } = new SessionService();
    public static IDatabaseService Database { get; } = new LiteDBService();
    public static IWindowService Windows { get; } = new WindowService();
}
