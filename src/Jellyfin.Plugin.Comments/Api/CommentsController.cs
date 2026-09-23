using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Comments.Data;
using Jellyfin.Plugin.Comments.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Comments.Api;

/// <summary>
/// Controller for handling comment-related API requests and serving live frontend files.
/// </summary>
[ApiController]
[Produces("application/json")]
public class CommentsController : ControllerBase
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUserManager _userManager;
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentsController"/> class.
    /// </summary>
    public CommentsController(
        ICommentRepository commentRepository, 
        IUserManager userManager, 
        IApplicationPaths applicationPaths)
    {
        _commentRepository = commentRepository;
        _userManager = userManager;
        _applicationPaths = applicationPaths;
    }

    /// <summary>
    /// Serves the frontend comments.js script directly from config/comments-ui.
    /// Route: GET /Comments/ClientScript.js
    /// </summary>
    [HttpGet("Comments/ClientScript.js")]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var scriptPath = Path.Combine(_applicationPaths.ConfigurationDirectoryPath, "comments-ui", "comments.js");
        if (System.IO.File.Exists(scriptPath))
        {
            return PhysicalFile(scriptPath, "application/javascript");
        }

        return NotFound("comments.js not found on disk.");
    }

    /// <summary>
    /// Serves the frontend comments.css styling directly from config/comments-ui.
    /// Route: GET /Comments/Styles.css
    /// </summary>
    [HttpGet("Comments/Styles.css")]
    [Produces("text/css")]
    public ActionResult GetStyles()
    {
        var cssPath = Path.Combine(_applicationPaths.ConfigurationDirectoryPath, "comments-ui", "comments.css");
        if (System.IO.File.Exists(cssPath))
        {
            return PhysicalFile(cssPath, "text/css");
        }

        return NotFound("comments.css not found on disk.");
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
        var storedComments = await _commentRepository.GetCommentsForItemAsync(itemId, cancellationToken);
        var currentUserId = GetCurrentUserId();

        var dtoDictionary = storedComments.ToDictionary(c => c.Id, c => new CommentDto
        {
            Id = c.Id,
            ItemId = c.ItemId,
            UserId = c.UserId,
            UserName = _userManager.GetUserById(c.UserId)?.Username ?? "Unknown User",
            ParentCommentId = c.ParentCommentId,
            Text = c.Text,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            CanDelete = c.UserId == currentUserId && (Plugin.Instance?.Configuration.AllowUsersToDeleteOwnComments ?? false)
        });

        var rootComments = new List<CommentDto>();
        foreach (var dto in dtoDictionary.Values)
        {
            if (dto.ParentCommentId.HasValue && dtoDictionary.TryGetValue(dto.ParentCommentId.Value, out var parentDto))
            {
                parentDto.Replies.Add(dto);
            }
            else
            {
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CommentDto>> CreateComment(
        [FromRoute] Guid itemId, 
        [FromBody] CreateCommentRequest request, 
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        if (!config.AllowUserComments)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Comment text cannot be empty.");

        if (request.Text.Length > config.MaxCommentLength)
            return BadRequest($"Comment exceeds maximum length of {config.MaxCommentLength} characters.");

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized("User is not authenticated or could not be resolved.");
        }

        var newComment = new StoredComment
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            UserId = userId,
            ParentCommentId = request.ParentCommentId,
            Text = request.Text.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _commentRepository.AddCommentAsync(newComment, cancellationToken);

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

        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>
    /// Deletes a comment by ID.
    /// Route: DELETE /Comments/{commentId}
    /// </summary>
    [HttpDelete("Comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        if (userId == Guid.Empty)
            return Unauthorized();

        var config = Plugin.Instance!.Configuration;
        if (existingComment.UserId != userId || !config.AllowUsersToDeleteOwnComments)
        {
            return Forbid(); 
        }

        await _commentRepository.DeleteCommentAsync(commentId, cancellationToken);

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var claimValue = User.Claims.FirstOrDefault(c => 
            c.Type == ClaimTypes.NameIdentifier || 
            c.Type == "UserId" || 
            c.Type.EndsWith("userid", StringComparison.OrdinalIgnoreCase) ||
            c.Type.EndsWith("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

        if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out var userIdFromClaim))
        {
            return userIdFromClaim;
        }

        if (!string.IsNullOrEmpty(User.Identity?.Name))
        {
            var user = _userManager.GetUserByName(User.Identity.Name);
            if (user != null)
            {
                return user.Id;
            }
        }

        foreach (var claim in User.Claims)
        {
            if (Guid.TryParse(claim.Value, out var parsedId))
            {
                var user = _userManager.GetUserById(parsedId);
                if (user != null)
                {
                    return user.Id;
                }
            }
        }

        return Guid.Empty;
    }
}