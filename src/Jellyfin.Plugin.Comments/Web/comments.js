/* Jellyfin Comments Plugin - Frontend Integration
 * Jellyfin Comments Frontend Version: 0.1.9.0
 */
(function () {
    const ONLY_FOR_USER = null;

    const style = document.createElement("style");
    style.id = "jf-comments-style";
    style.innerHTML = `
        .jf-comments-container { margin: 2em 0 1em; width: 100%; padding: 1.2em; background: rgba(20,20,20,.7); border-radius: 10px; backdrop-filter: blur(10px); border: 1px solid rgba(255,255,255,.08); font-family: inherit; color: #eee; box-sizing: border-box; }
        .jf-comments-title { font-size: 1.25em; font-weight: 600; margin-bottom: .8em; display: flex; align-items: center; gap: 8px; }
        .jf-comment-input-box { display: flex; flex-direction: column; gap: 8px; margin-bottom: 1.5em; }
        .jf-comment-position-input { width: 150px; padding: 6px 8px; border-radius: 4px; background: rgba(0,0,0,.45); border: 1px solid rgba(255,255,255,.15); color: #fff; font: inherit; }
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
        .jf-comment-position { background: transparent; border: 0; padding: 0; color: #6bc5ee; font: inherit; cursor: pointer; text-decoration: underline; }
        .jf-comment-position:hover { color: #fff; }
        .jf-comment-text { font-size: .9em; line-height: 1.4; white-space: pre-wrap; word-break: break-word; margin: 4px 0 6px; }
        .jf-comment-actions { display: flex; gap: 8px; align-items: center; }
        .jf-replies-list { margin-left: 20px; border-left: 2px solid rgba(255,255,255,.12); padding-left: 12px; margin-top: 8px; }
        .jf-reply-box { margin-top: 8px; margin-bottom: 8px; }
        .jf-playback-comment-control { position: fixed; right: 20px; bottom: 92px; z-index: 2147483000; display: flex; flex-direction: column; align-items: flex-end; gap: 8px; width: min(340px, calc(100vw - 32px)); }
        .jf-playback-comment-panel { display: none; width: 100%; padding: 12px; background: rgba(18,18,18,.96); border: 1px solid rgba(255,255,255,.18); border-radius: 6px; box-shadow: 0 4px 20px rgba(0,0,0,.5); box-sizing: border-box; }
        .jf-playback-comment-panel textarea { min-height: 74px; }
        .jf-playback-comment-position { margin-bottom: 8px; color: #6bc5ee; font-size: .9em; }
        .jf-playback-comment-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
    `;
    if (!document.getElementById(style.id)) document.head.appendChild(style);

    function escapeHtml(value) {
        const div = document.createElement("div");
        div.innerText = value || "";
        return div.innerHTML;
    }

    function parsePosition(value) {
        const parts = value.trim().split(":");
        if (parts.length !== 2 && parts.length !== 3) return null;
        if (parts.some(part => !/^\d+$/.test(part))) return null;

        const numbers = parts.map(Number);
        const seconds = numbers[numbers.length - 1];
        const minutes = numbers[numbers.length - 2];
        if (seconds > 59 || (parts.length === 3 && minutes > 59)) return null;

        const totalSeconds = parts.length === 3
            ? numbers[0] * 3600 + minutes * 60 + seconds
            : minutes * 60 + seconds;
        const positionTicks = totalSeconds * 10000000;
        return Number.isSafeInteger(positionTicks) ? positionTicks : null;
    }

    function formatPosition(positionTicks) {
        const totalSeconds = Math.floor(positionTicks / 10000000);
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;
        const pad = value => String(value).padStart(2, "0");
        return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${pad(minutes)}:${pad(seconds)}`;
    }

    async function jumpToPosition(itemId, positionTicks) {
        const playback = window.playbackManager;
        let currentItem = null;
        try {
            currentItem = playback?.currentItem?.() || null;
        } catch {
            currentItem = null;
        }
        if (currentItem?.Id === itemId && typeof playback.seek === "function") {
            playback.seek(positionTicks);
            return;
        }

        if (typeof ApiClient.getSessions !== "function" || typeof ApiClient.sendPlayCommand !== "function") {
            throw new Error("Der Jellyfin-Web-Player stellt keine Wiedergabesteuerung bereit.");
        }

        const sessions = await ApiClient.getSessions();
        const deviceId = ApiClient.deviceId();
        const userId = ApiClient.getCurrentUserId();
        const session = sessions.find(candidate => candidate.DeviceId === deviceId && candidate.UserId === userId);
        if (!session) throw new Error("Die aktuelle Jellyfin-Web-Sitzung wurde nicht gefunden.");

        await ApiClient.sendPlayCommand(session.Id, {
            ItemIds: itemId,
            PlayCommand: "PlayNow",
            StartPositionTicks: positionTicks
        });
    }

    function getActiveVideo() {
        return Array.from(document.querySelectorAll("video")).find(video => {
            const bounds = video.getBoundingClientRect();
            return bounds.width > 0 && bounds.height > 0 && video.readyState > 0 && !video.ended;
        }) || null;
    }

    async function getCurrentPlaybackItemId() {
        if (typeof ApiClient.getSessions !== "function") {
            throw new Error("Jellyfin kann die aktuelle Wiedergabesitzung nicht ermitteln.");
        }

        const sessions = await ApiClient.getSessions();
        const deviceId = ApiClient.deviceId();
        const userId = ApiClient.getCurrentUserId();
        const session = sessions.find(candidate => candidate.DeviceId === deviceId
            && candidate.UserId === userId && candidate.NowPlayingItem?.Id);
        if (!session) throw new Error("Der aktuell abgespielte Film wurde nicht gefunden.");
        return session.NowPlayingItem.Id;
    }

    function updatePlaybackCommentControl() {
        const video = getActiveVideo();
        let control = document.getElementById("jf-playback-comment-control");
        if (!video) {
            control?.remove();
            return;
        }

        const host = video.closest(".videoPlayerContainer") || document.body;
        if (!control) {
            control = document.createElement("div");
            control.id = "jf-playback-comment-control";
            control.className = "jf-playback-comment-control";
            control.innerHTML = `
                <div class="jf-playback-comment-panel" data-role="playback-panel">
                    <div class="jf-playback-comment-position" data-role="playback-position"></div>
                    <textarea class="jf-comment-textarea" data-role="playback-text" placeholder="Kommentar zu dieser Stelle..."></textarea>
                    <div class="jf-playback-comment-actions">
                        <button class="jf-comment-btn-secondary" data-role="playback-cancel">Abbrechen</button>
                        <button class="jf-comment-btn" data-role="playback-submit">Kommentieren</button>
                    </div>
                </div>
                <button class="jf-comment-btn" data-role="playback-open">💬 Kommentar bei 00:00</button>`;

            const panel = control.querySelector('[data-role="playback-panel"]');
            const positionLabel = control.querySelector('[data-role="playback-position"]');
            const textInput = control.querySelector('[data-role="playback-text"]');
            const openButton = control.querySelector('[data-role="playback-open"]');
            const submitButton = control.querySelector('[data-role="playback-submit"]');

            openButton.onclick = async () => {
                const activeVideo = getActiveVideo();
                if (!activeVideo) return;
                openButton.disabled = true;
                try {
                    const sourceAtClick = activeVideo.currentSrc;
                    const positionTicks = Math.max(0, Math.round(activeVideo.currentTime * 10000000));
                    const itemId = await getCurrentPlaybackItemId();
                    if (getActiveVideo() !== activeVideo || activeVideo.currentSrc !== sourceAtClick) {
                        throw new Error("Die Wiedergabe hat währenddessen das Medium gewechselt. Bitte erneut klicken.");
                    }
                    control.dataset.itemId = itemId;
                    control.dataset.positionTicks = String(positionTicks);
                    positionLabel.textContent = `Zeitmarke: ${formatPosition(positionTicks)}`;
                    panel.style.display = "block";
                    textInput.focus();
                } catch (error) {
                    console.error("Could not get current playback context:", error);
                    alert(error.message || "Aktuelle Wiedergabe konnte nicht ermittelt werden.");
                } finally {
                    openButton.disabled = false;
                }
            };

            control.querySelector('[data-role="playback-cancel"]').onclick = () => {
                panel.style.display = "none";
                textInput.value = "";
            };

            submitButton.onclick = async () => {
                const text = textInput.value.trim();
                if (!text) return;
                submitButton.disabled = true;
                try {
                    const response = await jfApi(`/Items/${control.dataset.itemId}/Comments`, {
                        method: "POST",
                        body: JSON.stringify({ text, parentCommentId: null, positionTicks: Number(control.dataset.positionTicks) })
                    });
                    if (response.ok) {
                        textInput.value = "";
                        panel.style.display = "none";
                        openButton.textContent = `💬 Kommentar bei ${positionLabel.textContent.replace("Zeitmarke: ", "")}`;
                    } else {
                        alert(`Kommentar konnte nicht gespeichert werden (HTTP ${response.status}).`);
                    }
                } catch (error) {
                    console.error("Could not save playback comment:", error);
                    alert("Kommentar konnte wegen eines Netzwerkfehlers nicht gespeichert werden.");
                } finally {
                    submitButton.disabled = false;
                }
            };
        }

        if (control.parentElement !== host) host.appendChild(control);
        if (control.querySelector('[data-role="playback-panel"]').style.display !== "block") {
            const currentTicks = Math.max(0, Math.round(video.currentTime * 10000000));
            control.querySelector('[data-role="playback-open"]').textContent = `💬 Kommentar bei ${formatPosition(currentTicks)}`;
        }
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
                <input class="jf-comment-position-input" data-role="main-position" type="text" inputmode="numeric" placeholder="Zeit (MM:SS)" aria-label="Optionale Filmposition, zum Beispiel 34:34">
                <button class="jf-comment-btn" data-role="main-submit">Kommentieren</button>
            </div>
            <div data-role="comments-list"></div>`;

        const mainInput = container.querySelector('[data-role="main-input"]');
        const mainPosition = container.querySelector('[data-role="main-position"]');
        const mainButton = container.querySelector('[data-role="main-submit"]');
        mainButton.onclick = async () => {
            const text = mainInput.value.trim();
            if (!text) return;
            const positionTicks = mainPosition.value.trim() ? parsePosition(mainPosition.value) : null;
            if (mainPosition.value.trim() && positionTicks === null) {
                alert("Zeit ungültig. Bitte MM:SS oder HH:MM:SS eingeben, zum Beispiel 34:34.");
                return;
            }
            mainButton.disabled = true;
            const response = await jfApi(`/Items/${itemId}/Comments`, {
                method: "POST",
                body: JSON.stringify({ text, parentCommentId: null, positionTicks })
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
        const positionTicks = Number.isSafeInteger(comment.PositionTicks) && comment.PositionTicks >= 0 ? comment.PositionTicks : null;
        const positionText = positionTicks === null ? "" : formatPosition(positionTicks);
        item.innerHTML = `
            <div class="jf-comment-header"><div><span class="jf-comment-user">${escapeHtml(comment.UserName || "Unbekannt")}</span><span class="jf-comment-date">${dateText}</span>${positionTicks === null ? "" : ` <button class="jf-comment-position" data-role="jump-position" title="Ab ${positionText} abspielen">${positionText}</button>`}</div>
                ${comment.CanDelete ? '<button class="jf-comment-btn-delete" title="Löschen">Löschen</button>' : ""}
            </div>
            <div class="jf-comment-text">${escapeHtml(comment.Text)}</div>
            <div class="jf-comment-actions"><button class="jf-comment-btn-secondary" data-role="reply-toggle">Antworten</button></div>
            <div class="jf-reply-box" data-role="reply-box" style="display:none">
                <textarea class="jf-comment-textarea" style="min-height:50px;margin-top:8px" placeholder="Antwort verfassen..."></textarea>
                <input class="jf-comment-position-input" data-role="reply-position" type="text" inputmode="numeric" placeholder="Zeit (MM:SS)" aria-label="Optionale Filmposition für die Antwort">
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

        const positionButton = item.querySelector('[data-role="jump-position"]');
        if (positionButton) {
            positionButton.onclick = async () => {
                positionButton.disabled = true;
                try {
                    await jumpToPosition(itemId, positionTicks);
                } catch (error) {
                    console.error("Could not start playback at comment position:", error);
                    alert(error.message || "Wiedergabe an dieser Position konnte nicht gestartet werden.");
                } finally {
                    positionButton.disabled = false;
                }
            };
        }

        const replyBox = item.querySelector('[data-role="reply-box"]');
        const replyInput = replyBox.querySelector("textarea");
        const replyPosition = replyBox.querySelector('[data-role="reply-position"]');
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
            const positionTicks = replyPosition.value.trim() ? parsePosition(replyPosition.value) : null;
            if (replyPosition.value.trim() && positionTicks === null) {
                alert("Zeit ungültig. Bitte MM:SS oder HH:MM:SS eingeben, zum Beispiel 34:34.");
                return;
            }
            replyButton.disabled = true;
            const response = await jfApi(`/Items/${itemId}/Comments`, {
                method: "POST",
                body: JSON.stringify({ text, parentCommentId: commentId, positionTicks })
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
    setInterval(() => {
        checkCurrentPage();
        updatePlaybackCommentControl();
    }, 1000);
})();
