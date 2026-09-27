/* Jellyfin Comments Plugin - Frontend Integration
 * Jellyfin Comments Frontend Version: 0.1.7.0
 */
(function () {
    const ONLY_FOR_USER = null;

    const style = document.createElement("style");
    style.id = "jf-comments-style";
    style.innerHTML = `
        .jf-comments-container { margin: 2em 0 1em; width: 100%; padding: 1.2em; background: rgba(20,20,20,.7); border-radius: 10px; backdrop-filter: blur(10px); border: 1px solid rgba(255,255,255,.08); font-family: inherit; color: #eee; box-sizing: border-box; }
        .jf-comments-title { font-size: 1.25em; font-weight: 600; margin-bottom: .8em; display: flex; align-items: center; gap: 8px; }
        .jf-comment-input-box { display: flex; flex-direction: column; gap: 8px; margin-bottom: 1.5em; }
        .jf-comment-textarea { width: 100%; min-height: 65px; padding: 10px 12px; border-radius: 6px; background: rgba(0,0,0,.45); border: 1px solid rgba(255,255,255,.15); color: #fff; resize: vertical; font-family: inherit; box-sizing: border-box; font-size: .95em; }
        .jf-comment-textarea:focus { outline: none; border-color: #00a4dc; }
        .jf-comment-btn { align-self: flex-end; padding: 6px 16px; border-radius: 4px; border: none; background: #00a4dc; color: #fff; font-weight: 600; cursor: pointer; transition: background .2s; }
        .jf-comment-btn:hover { background: #0085b2; }
        .jf-comment-btn:disabled { opacity: .6; cursor: wait; }
        .jf-comment-btn-secondary { background: transparent; color: #aaa; border: 1px solid rgba(255,255,255,.2); padding: 3px 8px; font-size: .8em; border-radius: 4px; cursor: pointer; }
        .jf-comment-btn-secondary:hover { background: rgba(255,255,255,.1); color: #fff; }
        .jf-comment-btn-delete { background: transparent; color: #e55353; border: none; font-size: .8em; cursor: pointer; padding: 2px 4px; }
        .jf-comment-btn-delete:hover { text-decoration: underline; }
        .jf-comment-item { border-bottom: 1px solid rgba(255,255,255,.08); padding: 10px 0; }
        .jf-comment-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
        .jf-comment-user { font-weight: 600; color: #00a4dc; font-size: .9em; }
        .jf-comment-date { font-size: .75em; color: #777; margin-left: 8px; }
        .jf-comment-text { font-size: .9em; line-height: 1.4; white-space: pre-wrap; word-break: break-word; margin: 4px 0 6px; }
        .jf-comment-actions { display: flex; gap: 8px; align-items: center; }
        .jf-replies-list { margin-left: 20px; border-left: 2px solid rgba(255,255,255,.12); padding-left: 12px; margin-top: 8px; }
        .jf-reply-box { margin-top: 8px; margin-bottom: 8px; }
    `;
    if (!document.getElementById(style.id)) document.head.appendChild(style);

    function escapeHtml(value) {
        const div = document.createElement("div");
        div.innerText = value || "";
        return div.innerHTML;
    }

    async function jfApi(path, options = {}) {
        if (!window.ApiClient) return { ok: false, status: 0 };
        const headers = {
            ...(options.headers || {}),
            "X-Emby-Token": ApiClient.accessToken()
        };
        if (options.body && !headers["Content-Type"]) headers["Content-Type"] = "application/json";
        const response = await fetch(ApiClient.getUrl(path), { ...options, headers });
        const text = await response.text();
        let body = null;
        try { body = text ? JSON.parse(text) : null; } catch { body = text; }
        return { ok: response.ok, status: response.status, body };
    }

    async function loadAndRenderComments(itemId, container) {
        container.innerHTML = '<div style="text-align:center;color:#777;padding:15px">Lade Kommentare...</div>';
        const response = await jfApi(`/Items/${itemId}/Comments`);
        if (!response.ok) {
            container.innerHTML = '<div style="color:#e55353">Fehler beim Laden der Kommentare.</div>';
            console.error("Comments load failed:", response.status, response.body);
            return;
        }
        renderCommentsUi(itemId, response.body || [], container);
    }

    function countAllComments(comments) {
        return comments.reduce((count, comment) => count + 1 + countAllComments(comment.Replies || []), 0);
    }

    function renderCommentsUi(itemId, comments, container) {
        container.innerHTML = `
            <div class="jf-comments-title"><span>💬 Kommentare</span><span style="font-size:.7em;color:#888">(${countAllComments(comments)})</span></div>
            <div class="jf-comment-input-box">
                <textarea class="jf-comment-textarea" data-role="main-input" placeholder="Schreibe einen Kommentar..."></textarea>
                <button class="jf-comment-btn" data-role="main-submit">Kommentieren</button>
            </div>
            <div data-role="comments-list"></div>`;

        const mainInput = container.querySelector('[data-role="main-input"]');
        const mainButton = container.querySelector('[data-role="main-submit"]');
        mainButton.onclick = async () => {
            const text = mainInput.value.trim();
            if (!text) return;
            mainButton.disabled = true;
            const response = await jfApi(`/Items/${itemId}/Comments`, {
                method: "POST",
                body: JSON.stringify({ text, parentCommentId: null })
            });
            if (response.ok) {
                await loadAndRenderComments(itemId, container);
            } else {
                alert(`Kommentar konnte nicht gesendet werden (HTTP ${response.status}).`);
                mainButton.disabled = false;
            }
        };

        const list = container.querySelector('[data-role="comments-list"]');
        if (comments.length === 0) {
            list.innerHTML = '<div style="color:#777;font-style:italic;padding:10px 0">Noch keine Kommentare vorhanden. Sei der Erste!</div>';
            return;
        }
        comments.forEach(comment => list.appendChild(createCommentElement(itemId, comment, container)));
    }

    function createCommentElement(itemId, comment, mainContainer) {
        const item = document.createElement("div");
        item.className = "jf-comment-item";
        const createdAt = comment.CreatedAt ? new Date(comment.CreatedAt) : null;
        const dateText = createdAt && !Number.isNaN(createdAt.getTime()) ? createdAt.toLocaleString("de-DE") : "";
        const commentId = comment.Id || comment.id;
        item.innerHTML = `
            <div class="jf-comment-header"><div><span class="jf-comment-user">${escapeHtml(comment.UserName || "Unbekannt")}</span><span class="jf-comment-date">${dateText}</span></div>
                ${comment.CanDelete ? '<button class="jf-comment-btn-delete" title="Löschen">Löschen</button>' : ""}
            </div>
            <div class="jf-comment-text">${escapeHtml(comment.Text)}</div>
            <div class="jf-comment-actions"><button class="jf-comment-btn-secondary" data-role="reply-toggle">Antworten</button></div>
            <div class="jf-reply-box" data-role="reply-box" style="display:none">
                <textarea class="jf-comment-textarea" style="min-height:50px;margin-top:8px" placeholder="Antwort verfassen..."></textarea>
                <div style="display:flex;justify-content:flex-end;gap:8px;margin-top:6px">
                    <button class="jf-comment-btn-secondary" data-role="cancel-reply">Abbrechen</button>
                    <button class="jf-comment-btn" data-role="submit-reply" style="padding:4px 12px;font-size:.85em">Antworten</button>
                </div>
            </div>
            <div class="jf-replies-list"></div>`;

        const deleteButton = item.querySelector(".jf-comment-btn-delete");
        if (deleteButton) {
            deleteButton.onclick = async () => {
                if (!confirm("Möchtest du diesen Kommentar wirklich löschen?")) return;
                deleteButton.disabled = true;
                const response = await jfApi(`/Comments/${commentId}`, { method: "DELETE" });
                if (response.ok) await loadAndRenderComments(itemId, mainContainer);
                else {
                    alert(`Löschen fehlgeschlagen (HTTP ${response.status}).`);
                    deleteButton.disabled = false;
                }
            };
        }

        const replyBox = item.querySelector('[data-role="reply-box"]');
        const replyInput = replyBox.querySelector("textarea");
        item.querySelector('[data-role="reply-toggle"]').onclick = () => {
            replyBox.style.display = replyBox.style.display === "none" ? "block" : "none";
            if (replyBox.style.display === "block") replyInput.focus();
        };
        item.querySelector('[data-role="cancel-reply"]').onclick = () => {
            replyBox.style.display = "none";
            replyInput.value = "";
        };
        const replyButton = item.querySelector('[data-role="submit-reply"]');
        replyButton.onclick = async () => {
            const text = replyInput.value.trim();
            if (!text) return;
            replyButton.disabled = true;
            const response = await jfApi(`/Items/${itemId}/Comments`, {
                method: "POST",
                body: JSON.stringify({ text, parentCommentId: commentId })
            });
            if (response.ok) await loadAndRenderComments(itemId, mainContainer);
            else {
                alert(`Antwort konnte nicht gesendet werden (HTTP ${response.status}).`);
                replyButton.disabled = false;
            }
        };

        const replies = item.querySelector(".jf-replies-list");
        (comment.Replies || []).forEach(reply => replies.appendChild(createCommentElement(itemId, reply, mainContainer)));
        return item;
    }

    function checkCurrentPage() {
        if (!window.ApiClient) return;
        if (ONLY_FOR_USER) {
            const user = ApiClient.getCurrentUser ? ApiClient.getCurrentUser() : null;
            const name = user?.Name || user?.name;
            if (name && name.toLowerCase() !== ONLY_FOR_USER.toLowerCase()) return;
        }

        const hash = window.location.hash || "";
        const params = new URLSearchParams(hash.includes("?") ? hash.split("?")[1] : "");
        const itemId = params.get("id");
        if (!itemId) {
            document.getElementById("jf-comments-wrapper")?.remove();
            return;
        }

        const detailPage = document.querySelector(".itemDetailPage:not(.hide)");
        const target = detailPage?.querySelector(".detailPagePrimaryContent") ||
            detailPage?.querySelector(".itemDetailsGroup")?.parentElement ||
            detailPage?.querySelector(".detailPagePrimaryContainer") ||
            detailPage?.querySelector(".detailPageContent") || detailPage;
        if (!target) return;

        let wrapper = document.getElementById("jf-comments-wrapper");
        if (wrapper?.getAttribute("data-item-id") === itemId) return;
        wrapper?.remove();
        wrapper = document.createElement("div");
        wrapper.id = "jf-comments-wrapper";
        wrapper.className = "jf-comments-container";
        wrapper.setAttribute("data-item-id", itemId);
        target.appendChild(wrapper);
        loadAndRenderComments(itemId, wrapper);
    }

    document.addEventListener("viewshow", checkCurrentPage);
    window.addEventListener("hashchange", checkCurrentPage);
    setInterval(checkCurrentPage, 1000);
})();
