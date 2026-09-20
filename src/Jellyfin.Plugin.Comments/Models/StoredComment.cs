using System;

namespace Jellyfin.Plugin.Comments.Models;

/// <summary>
/// Represents a comment as stored in the persistence layer.
/// </summary>
public class StoredComment
{
    /// <summary>
    /// Gets or sets the unique identifier of the comment.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the ID of the media item this comment belongs to.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who authored the comment.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the parent comment ID if this is a reply; otherwise, null.
    /// </summary>
    public Guid? ParentCommentId { get; set; }

    /// <summary>
    /// Gets or sets the comment text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the comment was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the comment was last edited (UTC).
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}