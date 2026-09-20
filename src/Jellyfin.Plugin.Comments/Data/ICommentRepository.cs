using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Comments.Models;

namespace Jellyfin.Plugin.Comments.Data;

/// <summary>
/// Defines the data access operations for comments.
/// </summary>
public interface ICommentRepository
{
    /// <summary>
    /// Initializes the database/storage (e.g., creating tables if they don't exist).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific comment by its ID.
    /// </summary>
    Task<StoredComment?> GetCommentAsync(Guid commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all comments for a specific media item.
    /// </summary>
    Task<IReadOnlyList<StoredComment>> GetCommentsForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new comment to the storage.
    /// </summary>
    Task AddCommentAsync(StoredComment comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing comment.
    /// </summary>
    Task UpdateCommentAsync(StoredComment comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a comment by its ID.
    /// </summary>
    Task DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
}