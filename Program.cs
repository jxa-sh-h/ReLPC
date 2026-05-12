using Avalonia;
using System;

namespace ReLPC;

class Program
{
    
    [STAThread]
    public static void Main(string[] args) 
    {
        try
        {
            Console.WriteLine("Starting app...");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
            Console.WriteLine("App started.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in Main: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

        public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            
            .WithInterFont()
            .LogToTrace();
}
