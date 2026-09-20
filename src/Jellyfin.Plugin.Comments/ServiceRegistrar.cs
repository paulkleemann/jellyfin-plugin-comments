using Jellyfin.Plugin.Comments.Data;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Comments;

/// <summary>
/// Registers plugin services into the Jellyfin dependency injection container.
/// </summary>
public class ServiceRegistrar : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // 1. Unser Repository für die API-Controller registrieren
        serviceCollection.AddSingleton<ICommentRepository, SqliteCommentRepository>();

        // 2. Den Startup-Task (Datenbank-Initialisierung) als Hosted Service registrieren
        serviceCollection.AddHostedService<PluginEntryPoint>();
    }
}