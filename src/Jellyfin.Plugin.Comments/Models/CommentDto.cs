using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Comments.Models;

/// <summary>
/// Data transfer object representing a comment sent to the client.
/// </summary>
public class CommentDto
{
    /// <summary>
    /// Gets or sets the comment ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the media item ID.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the author's user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the author's display name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent comment ID if this is a reply.
    /// </summary>
    public Guid? ParentCommentId { get; set; }

    /// <summary>
    /// Gets or sets the comment content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last updated timestamp (UTC).
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the requesting user is allowed to delete this comment.
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Gets or sets the child replies to this comment (hierarchical tree representation).
    /// </summary>
    public List<CommentDto> Replies { get; set; } = [];
}