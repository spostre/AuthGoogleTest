document.addEventListener("DOMContentLoaded", () => {
    const TOKEN_KEY = "authToken";

    let currentUser = null;

    const appContainer = document.querySelector(".app-container");
    const authHeaderAction = document.getElementById("auth-header-action");
    const userInfoSection = document.getElementById("user-info-section");
    const userBadge = document.getElementById("user-badge");
    const userName = document.getElementById("user-name");
    const userEmail = document.getElementById("user-email");
    const userAvatar = document.getElementById("user-avatar");
    const statusNoticeSection = document.getElementById("status-notice-section");
    const statusMessage = document.getElementById("status-message");
    const statusActions = document.getElementById("status-actions");
    const guestExtras = document.getElementById("guest-extras");
    const notesSection = document.getElementById("notes-section");
    const notesWelcome = document.getElementById("notes-welcome");
    const notesGrid = document.getElementById("notes-grid");
    const noteModal = document.getElementById("note-modal");
    const noteModalTitle = document.getElementById("note-modal-title");
    const noteSubmitBtn = document.getElementById("note-submit-btn");
    const btnShowCreateModal = document.getElementById("btn-show-create-modal");
    const btnCloseModal = document.getElementById("btn-close-modal");
    const btnCancelModal = document.getElementById("btn-cancel-modal");
    const noteForm = document.getElementById("note-form");
    const noteTitle = document.getElementById("note-title");
    const noteContent = document.getElementById("note-content");

    let editingNoteId = null;

    captureTokenFromUrl();
    checkSession();
    bindProviderButtons();

    function bindProviderButtons() {
        document.querySelectorAll(".btn-provider").forEach(btn => {
            btn.addEventListener("click", () => {});
        });
    }

    function captureTokenFromUrl() {
        const hash = window.location.hash;
        if (hash.startsWith("#token=")) {
            localStorage.setItem(TOKEN_KEY, hash.substring(7));
            window.history.replaceState(null, "", window.location.pathname);
        }
    }

    function saveToken(token) {
        localStorage.setItem(TOKEN_KEY, token);
    }

    function getAuthHeaders() {
        const token = localStorage.getItem(TOKEN_KEY);
        return token ? { Authorization: `Bearer ${token}` } : {};
    }

    function authFetch(url, options = {}) {
        return fetch(url, {
            ...options,
            headers: { ...getAuthHeaders(), ...(options.headers || {}) }
        });
    }

    function setUserAvatar(name, pictureUrl) {
        userAvatar.innerHTML = "";
        userAvatar.classList.remove("has-photo");

        if (pictureUrl) {
            const img = document.createElement("img");
            img.src = pictureUrl;
            img.alt = `Foto de ${name || "usuario"}`;
            img.referrerPolicy = "no-referrer";
            img.loading = "eager";
            img.decoding = "async";
            img.onerror = () => {
                userAvatar.classList.remove("has-photo");
                userAvatar.textContent = (name || "U").charAt(0).toUpperCase();
            };
            userAvatar.appendChild(img);
            userAvatar.classList.add("has-photo");
            return;
        }

        userAvatar.textContent = (name || "U").charAt(0).toUpperCase();
    }

    function getPictureUrl(user) {
        return user?.pictureUrl || user?.PictureUrl || null;
    }

    function getAuthProvider(user) {
        return user?.authProvider || user?.AuthProvider || "local";
    }

    function updateUserBadge(provider) {
        if (provider === "google") {
            userBadge.innerHTML = `<i class="fa-brands fa-google"></i> Cuenta de Google`;
        } else {
            userBadge.innerHTML = `<i class="fa-solid fa-envelope"></i> Cuenta con correo`;
        }
    }

    async function checkSession() {
        const token = localStorage.getItem(TOKEN_KEY);
        if (!token) {
            showGuestUI();
            return;
        }

        try {
            const response = await authFetch("/api/auth/current-user");
            if (response.status === 401) {
                localStorage.removeItem(TOKEN_KEY);
                showGuestUI();
                return;
            }

            currentUser = await response.json();
            updateUI();
        } catch (error) {
            console.error("Error al obtener la sesión:", error);
            showGuestUI();
        }
    }

    function updateUI() {
        if (!currentUser?.isAuthenticated) {
            showGuestUI();
            return;
        }

        appContainer.classList.add("is-authenticated");
        userInfoSection.classList.remove("hidden");
        userName.textContent = currentUser.nombre || "Usuario";
        userEmail.textContent = currentUser.email || "";
        updateUserBadge(getAuthProvider(currentUser));
        setUserAvatar(currentUser.nombre, getPictureUrl(currentUser));

        authHeaderAction.innerHTML = `
            <button class="btn btn-danger btn-sm" id="btn-logout" type="button">
                <i class="fa-solid fa-right-from-bracket"></i>
                <span>Cerrar sesión</span>
            </button>
        `;
        document.getElementById("btn-logout").addEventListener("click", logout);

        if (!currentUser.isRegistered) {
            showUnregisteredUI();
        } else {
            showRegisteredUI();
        }
    }

    async function logout() {
        try {
            await authFetch("/api/auth/logout", { method: "POST" });
        } catch (error) {
            console.error("Error al cerrar sesión:", error);
        } finally {
            localStorage.removeItem(TOKEN_KEY);
            currentUser = null;
            showGuestUI();
        }
    }

    function showGuestUI() {
        appContainer.classList.remove("is-authenticated");
        userInfoSection.classList.add("hidden");
        notesSection.classList.add("hidden");
        statusNoticeSection.classList.remove("hidden");
        if (guestExtras) guestExtras.classList.remove("hidden");

        statusMessage.textContent =
            "Inicia sesión con tu correo y contraseña o con Google para guardar tus notas en NotesCampus.";

        authHeaderAction.innerHTML = "";

        statusActions.innerHTML = `
            <div class="auth-tabs" role="tablist">
                <button type="button" class="auth-tab active" data-tab="login">Iniciar sesión</button>
                <button type="button" class="auth-tab" data-tab="register">Registrarse</button>
            </div>

            <form id="local-login-form" class="auth-form">
                <div class="form-group">
                    <label for="login-email">Correo</label>
                    <input type="email" id="login-email" required autocomplete="email" placeholder="tu@correo.com">
                </div>
                <div class="form-group">
                    <label for="login-password">Contraseña</label>
                    <input type="password" id="login-password" required autocomplete="current-password" placeholder="••••••••">
                </div>
                <p class="auth-form-error hidden" id="login-error"></p>
                <button type="submit" class="btn btn-primary btn-lg btn-block" id="btn-login-local">
                    <i class="fa-solid fa-right-to-bracket"></i> Entrar
                </button>
            </form>

            <form id="local-register-form" class="auth-form hidden">
                <div class="form-group">
                    <label for="register-name">Nombre</label>
                    <input type="text" id="register-name" required autocomplete="name" placeholder="Tu nombre">
                </div>
                <div class="form-group">
                    <label for="register-email">Correo</label>
                    <input type="email" id="register-email" required autocomplete="email" placeholder="tu@correo.com">
                </div>
                <div class="form-group">
                    <label for="register-password">Contraseña</label>
                    <input type="password" id="register-password" required minlength="6" autocomplete="new-password" placeholder="Mínimo 6 caracteres">
                </div>
                <p class="auth-form-error hidden" id="register-error"></p>
                <button type="submit" class="btn btn-accent btn-lg btn-block" id="btn-register-local-submit">
                    <i class="fa-solid fa-user-plus"></i> Crear cuenta
                </button>
            </form>

            <div class="divider auth-divider">
                <span>o con Google</span>
            </div>

            <a href="/api/auth/login" class="btn btn-secondary btn-lg btn-block">
                <i class="fa-brands fa-google"></i>
                Continuar con Google
            </a>
        `;

        bindGuestAuthForms();
    }

    function bindGuestAuthForms() {
        const tabs = statusActions.querySelectorAll(".auth-tab");
        const loginForm = document.getElementById("local-login-form");
        const registerForm = document.getElementById("local-register-form");
        const loginError = document.getElementById("login-error");
        const registerError = document.getElementById("register-error");

        tabs.forEach(tab => {
            tab.addEventListener("click", () => {
                tabs.forEach(t => t.classList.remove("active"));
                tab.classList.add("active");
                const isLogin = tab.dataset.tab === "login";
                loginForm.classList.toggle("hidden", !isLogin);
                registerForm.classList.toggle("hidden", isLogin);
                loginError.classList.add("hidden");
                registerError.classList.add("hidden");
            });
        });

        loginForm.addEventListener("submit", async (e) => {
            e.preventDefault();
            loginError.classList.add("hidden");
            const btn = document.getElementById("btn-login-local");
            const original = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Entrando...`;

            try {
                const response = await fetch("/api/auth/login-local", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        email: document.getElementById("login-email").value.trim(),
                        password: document.getElementById("login-password").value
                    })
                });

                const data = await response.json().catch(() => ({}));
                if (response.ok && data.token) {
                    saveToken(data.token);
                    await checkSession();
                    return;
                }

                loginError.textContent = data.message || data.title || data.detail || "Correo o contraseña incorrectos.";
                loginError.classList.remove("hidden");
            } catch (error) {
                console.error("Error al iniciar sesión:", error);
                loginError.textContent = "No se pudo conectar con el servidor.";
                loginError.classList.remove("hidden");
            } finally {
                btn.disabled = false;
                btn.innerHTML = original;
            }
        });

        registerForm.addEventListener("submit", async (e) => {
            e.preventDefault();
            registerError.classList.add("hidden");
            const btn = document.getElementById("btn-register-local-submit");
            const original = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Creando cuenta...`;

            try {
                const response = await fetch("/api/auth/register-local", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        nombre: document.getElementById("register-name").value.trim(),
                        email: document.getElementById("register-email").value.trim(),
                        password: document.getElementById("register-password").value
                    })
                });

                const data = await response.json().catch(() => ({}));
                if (response.ok && data.token) {
                    saveToken(data.token);
                    await checkSession();
                    return;
                }

                registerError.textContent = data.message || data.title || data.detail || "No se pudo crear la cuenta.";
                registerError.classList.remove("hidden");
            } catch (error) {
                console.error("Error al registrarse:", error);
                registerError.textContent = "No se pudo conectar con el servidor.";
                registerError.classList.remove("hidden");
            } finally {
                btn.disabled = false;
                btn.innerHTML = original;
            }
        });
    }

    function showUnregisteredUI() {
        notesSection.classList.add("hidden");
        statusNoticeSection.classList.remove("hidden");
        if (guestExtras) guestExtras.classList.add("hidden");

        statusActions.innerHTML = `
            <p class="auth-card-message">Hola <strong>${escapeHTML(currentUser.nombre)}</strong>, completa tu registro en NotesCampus para empezar a guardar notas.</p>
            <button class="btn btn-accent btn-lg btn-block" id="btn-register-google" type="button">
                <i class="fa-solid fa-user-plus"></i>
                Completar registro
            </button>
        `;

        document.getElementById("btn-register-google").addEventListener("click", registerGoogleUser);
    }

    async function registerGoogleUser() {
        const btn = document.getElementById("btn-register-google");
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Registrando...`;

        try {
            const response = await authFetch("/api/auth/register-google", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    googleId: currentUser.googleId,
                    nombre: currentUser.nombre,
                    email: currentUser.email
                })
            });

            const data = await response.json().catch(() => ({}));
            if (response.ok) {
                if (data.token) {
                    saveToken(data.token);
                }
                await checkSession();
            } else {
                btn.disabled = false;
                btn.innerHTML = `<i class="fa-solid fa-user-plus"></i> Completar registro`;
            }
        } catch (error) {
            console.error("Error al registrar usuario:", error);
            btn.disabled = false;
            btn.innerHTML = `<i class="fa-solid fa-user-plus"></i> Completar registro`;
        }
    }

    function showRegisteredUI() {
        statusNoticeSection.classList.add("hidden");
        notesSection.classList.remove("hidden");
        notesWelcome.textContent = `Hola, ${currentUser.nombre}. Aquí están tus notas.`;
        loadNotes();
    }

    async function loadNotes() {
        notesGrid.innerHTML = `
            <div class="state-card loading-state">
                <i class="fa-solid fa-circle-notch fa-spin"></i>
                <p>Cargando tus notas...</p>
            </div>
        `;

        try {
            const response = await authFetch("/api/notes");
            if (!response.ok) throw new Error("No se pudieron cargar las notas.");
            renderNotes(await response.json());
        } catch (error) {
            console.error("Error al cargar notas:", error);
            notesGrid.innerHTML = `
                <div class="state-card error-state">
                    <i class="fa-solid fa-circle-exclamation"></i>
                    <p>No se pudieron cargar tus notas. Intenta de nuevo.</p>
                </div>
            `;
        }
    }

    function renderNotes(notes) {
        notesGrid.innerHTML = "";

        if (notes.length === 0) {
            notesGrid.innerHTML = `
                <div class="state-card empty-state">
                    <i class="fa-regular fa-note-sticky"></i>
                    <h3>Aún no tienes notas</h3>
                    <p>Pulsa "Nueva Nota" para crear la primera.</p>
                </div>
            `;
            return;
        }

        notes.forEach(note => {
            const noteId = note.id ?? note.Id;
            const card = document.createElement("article");
            card.className = "note-card";
            card.dataset.noteId = noteId;
            card.innerHTML = `
                <div class="note-header-card">
                    <h3>${escapeHTML(note.titulo ?? note.Titulo)}</h3>
                </div>
                <p class="note-body-card">${escapeHTML(note.contenido ?? note.Contenido).replace(/\n/g, "<br>")}</p>
                <div class="note-footer-card">
                    <span class="note-date">
                        <i class="fa-regular fa-calendar"></i>
                        ${new Date(note.fechaCreacion ?? note.FechaCreacion).toLocaleDateString("es-ES", {
                            day: "numeric", month: "short", year: "numeric",
                            hour: "2-digit", minute: "2-digit"
                        })}
                    </span>
                    <div class="note-actions">
                        <button class="btn btn-icon btn-edit" type="button" data-note-id="${noteId}" aria-label="Editar nota">
                            <i class="fa-solid fa-pen"></i>
                        </button>
                        <button class="btn btn-icon btn-delete" type="button" data-note-id="${noteId}" aria-label="Eliminar nota">
                            <i class="fa-solid fa-trash"></i>
                        </button>
                    </div>
                </div>
            `;

            card.querySelector(".btn-edit").addEventListener("click", () => openEditModal(note));
            card.querySelector(".btn-delete").addEventListener("click", () => deleteNote(noteId, card));
            notesGrid.appendChild(card);
        });
    }

    function openCreateModal() {
        editingNoteId = null;
        noteModalTitle.textContent = "Crear Nueva Nota";
        noteSubmitBtn.innerHTML = `<i class="fa-solid fa-floppy-disk"></i> Guardar`;
        noteForm.reset();
        noteModal.classList.remove("hidden");
        noteTitle.focus();
    }

    function openEditModal(note) {
        editingNoteId = note.id ?? note.Id;
        noteModalTitle.textContent = "Editar Nota";
        noteSubmitBtn.innerHTML = `<i class="fa-solid fa-floppy-disk"></i> Actualizar`;
        noteTitle.value = note.titulo ?? note.Titulo ?? "";
        noteContent.value = note.contenido ?? note.Contenido ?? "";
        noteModal.classList.remove("hidden");
        noteTitle.focus();
    }

    async function deleteNote(noteId, cardEl) {
        if (cardEl) cardEl.remove();

        try {
            const response = await authFetch(`/api/notes/${noteId}`, { method: "DELETE" });
            if (!response.ok) {
                await loadNotes();
                return;
            }
            if (!notesGrid.querySelector(".note-card")) {
                renderNotes([]);
            }
        } catch (error) {
            console.error("Error al eliminar nota:", error);
            await loadNotes();
        }
    }

    btnShowCreateModal.addEventListener("click", openCreateModal);

    function hideModal() {
        noteModal.classList.add("hidden");
        noteForm.reset();
        editingNoteId = null;
        noteModalTitle.textContent = "Crear Nueva Nota";
        noteSubmitBtn.innerHTML = `<i class="fa-solid fa-floppy-disk"></i> Guardar`;
    }

    btnCloseModal.addEventListener("click", hideModal);
    btnCancelModal.addEventListener("click", hideModal);
    noteModal.addEventListener("click", (e) => {
        if (e.target === noteModal) hideModal();
    });

    noteForm.addEventListener("submit", async (e) => {
        e.preventDefault();

        const titleVal = noteTitle.value.trim();
        const contentVal = noteContent.value.trim();
        if (!titleVal || !contentVal) return;

        const submitBtn = noteSubmitBtn;
        const originalHtml = submitBtn.innerHTML;
        submitBtn.disabled = true;
        submitBtn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> ${editingNoteId ? "Actualizando..." : "Guardando..."}`;

        try {
            const isEditing = editingNoteId !== null;
            const response = await authFetch(
                isEditing ? `/api/notes/${editingNoteId}` : "/api/notes",
                {
                    method: isEditing ? "PUT" : "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ titulo: titleVal, contenido: contentVal })
                }
            );

            if (response.ok) {
                hideModal();
                await loadNotes();
            }
        } catch (error) {
            console.error("Error al guardar nota:", error);
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalHtml;
        }
    });

    function escapeHTML(str) {
        return String(str).replace(/[&<>'"]/g, tag => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
        }[tag] || tag));
    }
});
