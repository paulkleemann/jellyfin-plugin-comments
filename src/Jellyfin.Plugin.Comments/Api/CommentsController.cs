using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Comments.Data;
using Jellyfin.Plugin.Comments.Models;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Comments.Api;

/// <summary>
/// Controller for handling comment-related API requests.
/// </summary>
[ApiController] // Signalisiert .NET, dass diese Klasse API-Routen bereitstellt
[Authorize]     // Sichert die API ab: Nur eingeloggte Jellyfin-Nutzer dürfen hierauf zugreifen!
[Produces("application/json")] // Wir senden immer JSON zurück
public class CommentsController : ControllerBase
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentsController"/> class.
    /// Jellyfin injiziert das Repository und den UserManager automatisch (Dependency Injection).
    /// </summary>
    public CommentsController(ICommentRepository commentRepository, IUserManager userManager)
    {
        _commentRepository = commentRepository;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets all comments for a specific media item.
    /// Route: GET /Items/{itemId}/Comments
    /// </summary>
    [HttpGet("Items/{itemId:guid}/Comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(
        [FromRoute] Guid itemId, 
        CancellationToken cancellationToken)
    {
        // 1. Alle Kommentare für dieses Item (Film/Serie) aus der DB laden
        var storedComments = await _commentRepository.GetCommentsForItemAsync(itemId, cancellationToken);

        // Die ID des anfragenden Nutzers auslesen (um 'CanDelete' zu berechnen)
        var currentUserId = GetCurrentUserId();

        // 2. Umwandlung von 'StoredComment' in 'CommentDto' (Daten für das Frontend anreichern)
        var dtoDictionary = storedComments.ToDictionary(c => c.Id, c => new CommentDto
        {
            Id = c.Id,
            ItemId = c.ItemId,
            UserId = c.UserId,
            // Hier holen wir den aktuellen Namen des Nutzers. Falls der Nutzer gelöscht wurde, zeigen wir "Unknown User"
            UserName = _userManager.GetUserById(c.UserId)?.Username ?? "Unknown User",
            ParentCommentId = c.ParentCommentId,
            Text = c.Text,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            // Ein Nutzer darf seinen Kommentar löschen, wenn es in der Plugin-Config erlaubt ist
            CanDelete = c.UserId == currentUserId && (Plugin.Instance?.Configuration.AllowUsersToDeleteOwnComments ?? false)
        });

        // 3. Baumstruktur (Replies) aufbauen
        var rootComments = new List<CommentDto>();
        foreach (var dto in dtoDictionary.Values)
        {
            if (dto.ParentCommentId.HasValue && dtoDictionary.TryGetValue(dto.ParentCommentId.Value, out var parentDto))
            {
                // Es ist eine Antwort -> an den Eltern-Kommentar anhängen
                parentDto.Replies.Add(dto);
            }
            else
            {
                // Es ist ein Haupt-Kommentar -> zur Hauptliste hinzufügen
                rootComments.Add(dto);
            }
        }

        return Ok(rootComments);
    }

    /// <summary>
    /// Creates a new comment or reply.
    /// Route: POST /Items/{itemId}/Comments
    /// </summary>
    [HttpPost("Items/{itemId:guid}/Comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CommentDto>> CreateComment(
        [FromRoute] Guid itemId, 
        [FromBody] CreateCommentRequest request, 
        CancellationToken cancellationToken)
    {
        // Prüfen, ob normale Nutzer überhaupt kommentieren dürfen (Plugin-Config)
        var config = Plugin.Instance!.Configuration;
        if (!config.AllowUserComments)
        {
            // TODO: In einer perfekten Welt prüfen wir hier noch, ob der Nutzer ein Admin ist.
            // Für den Anfang blocken wir einfach, wenn es deaktiviert ist.
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Comment text cannot be empty.");

        if (request.Text.Length > config.MaxCommentLength)
            return BadRequest($"Comment exceeds maximum length of {config.MaxCommentLength} characters.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var newComment = new StoredComment
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            UserId = userId,
            ParentCommentId = request.ParentCommentId,
            Text = request.Text.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Ab in die Datenbank damit!
        await _commentRepository.AddCommentAsync(newComment, cancellationToken);

        // Wir geben den erstellten Kommentar direkt als DTO zurück, 
        // damit das Frontend ihn sofort ohne neuen GET-Request anzeigen kann.
        var dto = new CommentDto
        {
            Id = newComment.Id,
            ItemId = newComment.ItemId,
            UserId = newComment.UserId,
            UserName = _userManager.GetUserById(userId)?.Username ?? "Unknown User",
            ParentCommentId = newComment.ParentCommentId,
            Text = newComment.Text,
            CreatedAt = newComment.CreatedAt,
            CanDelete = config.AllowUsersToDeleteOwnComments
        };

        return CreatedAtAction(nameof(GetComments), new { itemId }, dto);
    }

    /// <summary>
    /// Deletes a comment by ID.
    /// Route: DELETE /Comments/{commentId}
    /// </summary>
    [HttpDelete("Comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(
        [FromRoute] Guid commentId, 
        CancellationToken cancellationToken)
    {
        var existingComment = await _commentRepository.GetCommentAsync(commentId, cancellationToken);
        if (existingComment == null)
            return NotFound();

        var userId = GetCurrentUserId();
        
        // Prüfen, ob es der eigene Kommentar ist und ob löschen erlaubt ist
        var config = Plugin.Instance!.Configuration;
        if (existingComment.UserId != userId || !config.AllowUsersToDeleteOwnComments)
        {
            return Forbid(); 
        }

        await _commentRepository.DeleteCommentAsync(commentId, cancellationToken);

        return NoContent(); // 204 No Content ist der Standard-Erfolgscode für DELETE
    }

    /// <summary>
    /// Hilfsmethode, um die User-ID des Nutzers auszulesen, der den HTTP-Request gesendet hat.
    /// Jellyfin setzt diese Info (Claims) automatisch anhand des Access-Tokens (Header).
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdString, out var userId) ? userId : Guid.Empty;
    }
}