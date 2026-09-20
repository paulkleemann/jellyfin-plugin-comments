using System;

namespace Jellyfin.Plugin.Comments.Models;

/// <summary>
/// Request payload for creating a new comment or reply.
/// </summary>
public class CreateCommentRequest
{
    /// <summary>
    /// Gets or sets the text content of the comment.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional parent comment ID to reply to.
    /// </summary>
    public Guid? ParentCommentId { get; set; }
}