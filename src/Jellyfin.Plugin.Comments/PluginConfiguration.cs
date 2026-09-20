using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Comments;

/// <summary>
/// Configuration for the Comments plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the maximum allowed length of a comment.
    /// </summary>
    public int MaxCommentLength { get; set; } = 2000;

    /// <summary>
    /// Gets or sets a value indicating whether regular users are allowed to create comments.
    /// </summary>
    public bool AllowUserComments { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether users are allowed to delete their own comments.
    /// </summary>
    public bool AllowUsersToDeleteOwnComments { get; set; } = true;
}