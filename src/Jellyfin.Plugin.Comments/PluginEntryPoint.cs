using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Comments.Data;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Comments;

/// <summary>
/// Hosted service that runs on Jellyfin startup to initialize DB, setup template UI files on disk, and auto-inject into index.html.
/// </summary>
public class PluginEntryPoint : IHostedService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<PluginEntryPoint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginEntryPoint"/> class.
    /// </summary>
    public PluginEntryPoint(
        ICommentRepository commentRepository, 
        IApplicationPaths applicationPaths, 
        ILogger<PluginEntryPoint> logger)
    {
        _commentRepository = commentRepository;
        _applicationPaths = applicationPaths;
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

        // 1. Erstelle den Ordner config/comments-ui mit Standard-Dateien, falls noch nicht vorhanden
        SetupFrontendFiles();

        // 2. Trage die Links automatisch in die index.html von Jellyfin ein
        InjectClientScript();
    }

    private void SetupFrontendFiles()
    {
        try
        {
            var uiDir = Path.Combine(_applicationPaths.ConfigurationDirectoryPath, "comments-ui");
            Directory.CreateDirectory(uiDir);

            var jsPath = Path.Combine(uiDir, "comments.js");
            var cssPath = Path.Combine(uiDir, "comments.css");

            // Falls comments.css noch nicht existiert -> Vorlage erstellen
            if (!File.Exists(cssPath))
            {
                const string defaultCss = """
                    /* Jellyfin Comments Plugin Styling */
                    #jellyfin-comments-plugin-root {
                        margin: 30px auto;
                        max-width: 900px;
                        padding: 20px;
                        background: rgba(0, 0, 0, 0.35);
                        border-radius: 12px;
                        box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
                        font-family: inherit;
                    }
                    .comment-card {
                        background: rgba(255, 255, 255, 0.05);
                        border-radius: 8px;
                        padding: 12px 16px;
                        margin-bottom: 12px;
                        border-left: 3px solid #00a4dc;
                    }
                    .comment-author {
                        color: #00a4dc;
                        font-weight: bold;
                    }
                    .comment-date {
                        color: #888;
                        font-size: 0.85em;
                    }
                    .comment-text {
                        color: #eee;
                        line-height: 1.4;
                        margin-top: 6px;
                        white-space: pre-wrap;
                        word-break: break-word;
                    }
                    #new-comment-text {
                        width: 100%;
                        box-sizing: border-box;
                        padding: 10px 12px;
                        border-radius: 8px;
                        background: rgba(255, 255, 255, 0.08);
                        border: 1px solid rgba(255, 255, 255, 0.2);
                        color: #fff;
                        font-size: 0.95em;
                        resize: vertical;
                    }
                    #submit-comment-btn {
                        background: #00a4dc;
                        color: #fff;
                        border: none;
                        border-radius: 6px;
                        padding: 8px 16px;
                        font-weight: bold;
                        cursor: pointer;
                        font-size: 0.9em;
                        transition: background 0.2s;
                    }
                    #submit-comment-btn:hover {
                        background: #0082b0;
                    }
                    """;
                File.WriteAllText(cssPath, defaultCss);
                _logger.LogInformation("Created template comments.css in {Path}", cssPath);
            }

            // Falls comments.js noch nicht existiert -> Vorlage erstellen
            if (!File.Exists(jsPath))
            {
                const string defaultJs = """
                    /**
                     * Jellyfin Comments Plugin - Live Frontend Script
                     */
                    (() => {
                        console.log("🍿 [Comments Plugin] Live Frontend Script aus config-Ordner geladen!");

                        function getAuthHeaders() {
                            const token = ApiClient.accessToken();
                            const deviceId = ApiClient.deviceId ? ApiClient.deviceId() : "Browser";
                            const appVersion = ApiClient.appVersion ? ApiClient.appVersion() : "10.11.7";
                            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="${appVersion}", Token="${token}"`;

                            return {
                                'Content-Type': 'application/json',
                                'Authorization': authHeader,
                                'X-Emby-Authorization': authHeader,
                                'X-Emby-Token': token
                            };
                        }

                        function getCurrentItemId() {
                            const hash = window.location.hash;
                            const match = hash.match(/[?&]id=([a-f0-9]+)/i);
                            return match ? match[1] : null;
                        }

                        async function loadComments(itemId, container) {
                            const listElement = container.querySelector('#comments-list');
                            listElement.innerHTML = '<div style="color: #888; padding: 10px;">Lade Kommentare...</div>';

                            try {
                                const url = ApiClient.getUrl(`/Items/${itemId}/Comments`);
                                const res = await fetch(url, { headers: getAuthHeaders() });
                                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                                const comments = await res.json();
                                renderComments(comments, listElement, itemId);
                            } catch (err) {
                                console.error("❌ Fehler beim Laden der Kommentare:", err);
                                listElement.innerHTML = '<div style="color: #ff6b6b; padding: 10px;">Kommentare konnten nicht geladen werden.</div>';
                            }
                        }

                        function renderComments(comments, listElement, itemId) {
                            if (!comments || comments.length === 0) {
                                listElement.innerHTML = '<div style="color: #888; font-style: italic; padding: 15px 0;">Noch keine Kommentare vorhanden. Schreibe den ersten! ✍️</div>';
                                return;
                            }

                            listElement.innerHTML = '';
                            comments.forEach(comment => {
                                listElement.appendChild(createCommentElement(comment, itemId));
                            });
                        }

                        function createCommentElement(comment, itemId) {
                            const div = document.createElement('div');
                            div.className = 'comment-card';

                            const date = new Date(comment.createdAt).toLocaleString('de-DE', {
                                day: '2-digit', month: '2-digit', year: 'numeric',
                                hour: '2-digit', minute: '2-digit'
                            });

                            div.innerHTML = `
                                <div style="display: flex; justify-content: space-between; font-size: 0.9em;">
                                    <span class="comment-author">${escapeHtml(comment.userName)}</span>
                                    <span class="comment-date">${date}</span>
                                </div>
                                <div class="comment-text">${escapeHtml(comment.text)}</div>
                            `;

                            if (comment.replies && comment.replies.length > 0) {
                                const repliesDiv = document.createElement('div');
                                repliesDiv.style.cssText = 'margin-left: 20px; margin-top: 10px; border-left: 2px solid #444; padding-left: 10px;';
                                comment.replies.forEach(reply => {
                                    repliesDiv.appendChild(createCommentElement(reply, itemId));
                                });
                                div.appendChild(repliesDiv);
                            }

                            return div;
                        }

                        async function submitComment(itemId, text, container) {
                            const submitBtn = container.querySelector('#submit-comment-btn');
                            const textarea = container.querySelector('#new-comment-text');

                            submitBtn.disabled = true;
                            submitBtn.innerText = 'Wird gesendet...';

                            try {
                                const url = ApiClient.getUrl(`/Items/${itemId}/Comments`);
                                const res = await fetch(url, {
                                    method: 'POST',
                                    headers: getAuthHeaders(),
                                    body: JSON.stringify({ text: text })
                                });

                                if (!res.ok) throw new Error(`HTTP ${res.status}`);

                                textarea.value = '';
                                await loadComments(itemId, container);
                            } catch (err) {
                                alert("Fehler beim Absenden des Kommentars!");
                                console.error(err);
                            } finally {
                                submitBtn.disabled = false;
                                submitBtn.innerText = 'Kommentar veröffentlichen';
                            }
                        }

                        function injectCommentsSection() {
                            const itemId = getCurrentItemId();
                            if (!itemId) return;

                            const targetContainer = document.querySelector('.detailPagePrimaryContainer') 
                                                 || document.querySelector('.itemDetailPage')
                                                 || document.querySelector('.detailSection');

                            if (!targetContainer || document.getElementById('jellyfin-comments-plugin-root')) {
                                return;
                            }

                            const section = document.createElement('div');
                            section.id = 'jellyfin-comments-plugin-root';

                            section.innerHTML = `
                                <h2 style="margin-top: 0; margin-bottom: 15px; color: #fff; font-size: 1.4em;">💬 Kommentare</h2>
                                <div style="margin-bottom: 20px;">
                                    <textarea id="new-comment-text" placeholder="Schreibe einen Kommentar zu diesem Film..." rows="3"></textarea>
                                    <div style="display: flex; justify-content: flex-end; margin-top: 8px;">
                                        <button id="submit-comment-btn">Kommentar veröffentlichen</button>
                                    </div>
                                </div>
                                <div id="comments-list"></div>
                            `;

                            const submitBtn = section.querySelector('#submit-comment-btn');
                            const textarea = section.querySelector('#new-comment-text');

                            submitBtn.addEventListener('click', () => {
                                const text = textarea.value.trim();
                                if (text) {
                                    submitComment(itemId, text, section);
                                }
                            });

                            targetContainer.appendChild(section);
                            loadComments(itemId, section);
                        }

                        function escapeHtml(str) {
                            if (!str) return '';
                            return str.replace(/[&<>'"]/g, 
                                tag => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[tag] || tag)
                            );
                        }

                        document.addEventListener('viewshow', () => setTimeout(injectCommentsSection, 400));
                        window.addEventListener('hashchange', () => setTimeout(injectCommentsSection, 400));
                        setTimeout(injectCommentsSection, 1200);
                    })();
                    """;
                File.WriteAllText(jsPath, defaultJs);
                _logger.LogInformation("Created template comments.js in {Path}", jsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup frontend template files in config/comments-ui.");
        }
    }

    private void InjectClientScript()
    {
        try
        {
            var possiblePaths = new List<string?>
            {
                _applicationPaths.WebPath,
                "/usr/share/jellyfin/web",
                "/jellyfin/jellyfin-web",
                Path.Combine(AppContext.BaseDirectory, "jellyfin-web")
            };

            const string tags = """
                <link rel="stylesheet" href="Comments/Styles.css">
                <script src="Comments/ClientScript.js" defer></script>
                """;

            foreach (var dir in possiblePaths)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                var indexPath = Path.Combine(dir, "index.html");
                if (!File.Exists(indexPath)) continue;

                var indexContent = File.ReadAllText(indexPath);

                if (!indexContent.Contains("Comments/ClientScript.js", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Injecting Comments tags into {IndexPath}...", indexPath);
                    var newContent = indexContent.Replace("</body>", $"{tags}\n</body>", StringComparison.OrdinalIgnoreCase);
                    File.WriteAllText(indexPath, newContent);
                    _logger.LogInformation("Comments script & css successfully injected into index.html!");
                }
                else
                {
                    _logger.LogInformation("Comments tags already present in {IndexPath}.", indexPath);
                }

                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-inject Comments tags into index.html.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}