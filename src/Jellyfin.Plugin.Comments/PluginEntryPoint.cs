using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Comments.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Comments;

/// <summary>
/// Hosted service that runs when the Jellyfin server starts.
/// </summary>
public class PluginEntryPoint : IHostedService
{
    private readonly ICommentRepository _commentRepository;
    private readonly ILogger<PluginEntryPoint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginEntryPoint"/> class.
    /// </summary>
    public PluginEntryPoint(ICommentRepository commentRepository, ILogger<PluginEntryPoint> logger)
    {
        _commentRepository = commentRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing Comments plugin database...");

        try
        {
            await _commentRepository.InitializeAsync(cancellationToken);
            _logger.LogInformation("Comments plugin database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Comments plugin database.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}