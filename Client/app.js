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

    const comingSoonModal = document.getElementById("coming-soon-modal");
    const btnCloseComingSoon = document.getElementById("btn-close-coming-soon");
    const btnComingSoonOk = document.getElementById("btn-coming-soon-ok");

    const settingsModal = document.getElementById("settings-modal");
    const btnCloseSettings = document.getElementById("btn-close-settings");
    const btnCancelSettings = document.getElementById("btn-cancel-settings");
    const passwordForm = document.getElementById("password-form");
    const currentPasswordGroup = document.getElementById("current-password-group");
    const settingsPasswordError = document.getElementById("settings-password-error");
    const settingsAvatarPreview = document.getElementById("settings-avatar-preview");
    const settingsPictureError = document.getElementById("settings-picture-error");
    const profilePictureUrlForm = document.getElementById("profile-picture-url-form");
    const profilePictureFileForm = document.getElementById("profile-picture-file-form");
    const btnRemovePicture = document.getElementById("btn-remove-picture");

    captureTokenFromUrl();
    handleAuthErrorsFromUrl();
    checkSession();
    bindProviderButtons();
    bindComingSoonModal();
    bindSettingsModal();

    function showComingSoonModal() {
        comingSoonModal?.classList.remove("hidden");
    }

    function hideComingSoonModal() {
        comingSoonModal?.classList.add("hidden");
    }

    function bindComingSoonModal() {
        btnCloseComingSoon?.addEventListener("click", hideComingSoonModal);
        btnComingSoonOk?.addEventListener("click", hideComingSoonModal);
        comingSoonModal?.addEventListener("click", (e) => {
            if (e.target === comingSoonModal) hideComingSoonModal();
        });
    }

    function bindProviderButtons() {
        document.querySelectorAll("[data-provider]").forEach(btn => {
            if (btn.dataset.provider === "google") {
                return;
            }

            btn.addEventListener("click", (e) => {
                e.preventDefault();
                showComingSoonModal();
            });
        });
    }

    function captureTokenFromUrl() {
        const hash = window.location.hash;
        if (!hash || hash.length < 2) {
            return;
        }

        const params = new URLSearchParams(hash.slice(1));
        const token = params.get("token");
        if (!token) {
            return;
        }

        localStorage.setItem(TOKEN_KEY, token);
        if (params.get("linked") === "google") {
            window.__authSuccessMessage =
                "Google vinculado. Ya puedes iniciar sesión con Google o con tu contraseña.";
        }

        window.history.replaceState(null, "", window.location.pathname);
    }

    function handleAuthErrorsFromUrl() {
        const params = new URLSearchParams(window.location.search);
        const error = params.get("error");
        if (!error) {
            return;
        }

        const messages = {
            email_already_registered:
                "Ese correo ya tiene cuenta con contraseña. Inicia sesión con correo y contraseña o vincula Google desde Configuración.",
            google_link_failed:
                "No se pudo vincular Google. Usa la misma cuenta de Google que tu correo registrado.",
            google_already_linked: "Tu cuenta ya tiene Google vinculado.",
            link_requires_login:
                "Inicia sesión con tu correo y contraseña para vincular Google.",
            no_email: "Google no devolvió un correo. No se pudo crear la cuenta.",
            auth_failed: "No se pudo completar el inicio de sesión con Google.",
            no_google_id: "No se recibió el identificador de Google."
        };

        if (messages[error]) {
            window.__authErrorMessage = messages[error];
        }

        window.history.replaceState(null, "", window.location.pathname);
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

    function userHasGoogle(user) {
        return Boolean(user?.hasGoogle || user?.googleId);
    }

    function userHasPassword(user) {
        return Boolean(user?.hasPassword);
    }

    function updateUserBadge(user) {
        const hasGoogle = userHasGoogle(user);
        const hasPassword = userHasPassword(user);

        if (hasGoogle && hasPassword) {
            userBadge.innerHTML = `<i class="fa-solid fa-shield-halved"></i> Google y correo`;
        } else if (hasGoogle) {
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
        updateUserBadge(currentUser);
        setUserAvatar(currentUser.nombre, getPictureUrl(currentUser));

        authHeaderAction.innerHTML = `
            <button class="btn btn-secondary btn-sm" id="btn-settings" type="button" title="Configuración">
                <i class="fa-solid fa-gear"></i>
                <span>Configuración</span>
            </button>
            <button class="btn btn-danger btn-sm" id="btn-logout" type="button">
                <i class="fa-solid fa-right-from-bracket"></i>
                <span>Cerrar sesión</span>
            </button>
        `;
        document.getElementById("btn-settings").addEventListener("click", openSettingsModal);
        document.getElementById("btn-logout").addEventListener("click", logout);

        if (!currentUser.isRegistered) {
            registerGoogleUser();
            return;
        }

        showRegisteredUI();
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

        statusMessage.textContent =
            window.__authErrorMessage ||
            "Inicia sesión con correo y contraseña o con Google para guardar y consultar tus notas.";
        delete window.__authErrorMessage;

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
                <div class="auth-form-row">
                    <div class="form-group">
                        <label for="register-name">Nombre</label>
                        <input type="text" id="register-name" required autocomplete="name" placeholder="Tu nombre">
                    </div>
                    <div class="form-group">
                        <label for="register-email">Correo</label>
                        <input type="email" id="register-email" required autocomplete="email" placeholder="tu@correo.com">
                    </div>
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

    async function registerGoogleUser() {
        notesSection.classList.add("hidden");
        statusNoticeSection.classList.remove("hidden");
        statusActions.innerHTML = `
            <p class="auth-card-message">
                <i class="fa-solid fa-spinner fa-spin"></i>
                Creando tu cuenta...
            </p>
        `;

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
                return;
            }

            statusActions.innerHTML = `
                <p class="auth-card-message">${escapeHTML(data.message || "No se pudo crear la cuenta.")}</p>
            `;
        } catch (error) {
            console.error("Error al registrar usuario:", error);
            statusActions.innerHTML = `
                <p class="auth-card-message">No se pudo conectar con el servidor.</p>
            `;
        }
    }

    function showRegisteredUI() {
        statusNoticeSection.classList.add("hidden");
        notesSection.classList.remove("hidden");
        notesWelcome.textContent = window.__authSuccessMessage
            ? window.__authSuccessMessage
            : `Hola, ${currentUser.nombre}. Aquí están tus notas.`;
        delete window.__authSuccessMessage;
        loadNotes();
    }

    function bindSettingsModal() {
        btnCloseSettings?.addEventListener("click", closeSettingsModal);
        btnCancelSettings?.addEventListener("click", closeSettingsModal);
        settingsModal?.addEventListener("click", (e) => {
            if (e.target === settingsModal) {
                closeSettingsModal();
            }
        });

        passwordForm?.addEventListener("submit", async (e) => {
            e.preventDefault();
            await savePasswordFromSettings();
        });

        profilePictureUrlForm?.addEventListener("submit", async (e) => {
            e.preventDefault();
            await savePictureUrlFromSettings();
        });

        profilePictureFileForm?.addEventListener("submit", async (e) => {
            e.preventDefault();
            await uploadPictureFromSettings();
        });

        btnRemovePicture?.addEventListener("click", removePictureFromSettings);

        document.getElementById("btn-link-google")?.addEventListener("click", linkGoogleAccount);
    }

    function linkGoogleAccount() {
        const token = localStorage.getItem(TOKEN_KEY);
        if (!token) {
            window.__authErrorMessage =
                "Inicia sesión con tu correo y contraseña para vincular Google.";
            showGuestUI();
            return;
        }

        window.location.href = `/api/auth/link-google?token=${encodeURIComponent(token)}`;
    }

    function setSettingsAvatarPreview(name, pictureUrl) {
        if (!settingsAvatarPreview) {
            return;
        }

        settingsAvatarPreview.innerHTML = "";
        settingsAvatarPreview.classList.remove("has-photo");

        if (pictureUrl) {
            const img = document.createElement("img");
            img.src = pictureUrl;
            img.alt = "Foto de perfil";
            settingsAvatarPreview.appendChild(img);
            settingsAvatarPreview.classList.add("has-photo");
            return;
        }

        settingsAvatarPreview.textContent = (name || "U").charAt(0).toUpperCase();
    }

    function applyProfilePictureResponse(data) {
        if (data.token) {
            saveToken(data.token);
        }

        if (data.user) {
            currentUser = { ...currentUser, ...data.user, isAuthenticated: true, isRegistered: true };
            setUserAvatar(currentUser.nombre, getPictureUrl(currentUser));
            updateUserBadge(currentUser);
        }
    }

    function closeSettingsModal() {
        settingsModal?.classList.add("hidden");
    }

    async function loadAccountSecurity() {
        const response = await authFetch("/api/auth/account-security");
        if (!response.ok) {
            throw new Error("No se pudo cargar la configuración de la cuenta.");
        }
        return response.json();
    }

    function renderSettingsPanel(security) {
        const googleStatus = document.getElementById("access-google-status");
        const passwordStatus = document.getElementById("access-password-status");
        const accessHint = document.getElementById("access-hint");
        const btnLinkGoogle = document.getElementById("btn-link-google");
        const passwordSectionTitle = document.getElementById("password-section-title");
        const newPasswordLabel = document.getElementById("settings-new-password-label");
        const saveBtn = document.getElementById("btn-save-password");

        const hasGoogle = Boolean(security.hasGoogle || security.googleId);
        const hasPassword = Boolean(security.hasPassword);

        googleStatus.textContent = hasGoogle ? "Activo" : "No vinculado";
        googleStatus.className = `access-method-status ${hasGoogle ? "is-active" : "is-inactive"}`;

        passwordStatus.textContent = hasPassword ? "Activo" : "Sin configurar";
        passwordStatus.className = `access-method-status ${hasPassword ? "is-active" : "is-inactive"}`;

        if (hasGoogle && !hasPassword) {
            accessHint.textContent =
                `Agrega una contraseña para entrar también con ${security.email} sin usar Google.`;
        } else if (hasPassword && hasGoogle) {
            accessHint.textContent =
                "Puedes iniciar sesión con Google o con tu correo y contraseña.";
        } else if (hasPassword) {
            accessHint.textContent =
                "Vincula Google con el mismo correo que usaste al registrarte para entrar también con Google.";
        } else {
            accessHint.textContent = "";
        }

        btnLinkGoogle?.classList.toggle("hidden", hasGoogle);

        currentPasswordGroup.classList.toggle("hidden", !hasPassword);
        passwordSectionTitle.textContent = hasPassword ? "Cambiar contraseña" : "Crear contraseña";
        newPasswordLabel.textContent = hasPassword ? "Nueva contraseña" : "Contraseña";
        saveBtn.innerHTML = hasPassword
            ? `<i class="fa-solid fa-floppy-disk"></i> Actualizar contraseña`
            : `<i class="fa-solid fa-key"></i> Activar acceso con contraseña`;

        setSettingsAvatarPreview(security.nombre, security.pictureUrl);
        const pictureUrlInput = document.getElementById("settings-picture-url");
        if (pictureUrlInput) {
            pictureUrlInput.value = security.pictureUrl || "";
        }
    }

    async function openSettingsModal() {
        settingsPasswordError.classList.add("hidden");
        settingsPictureError?.classList.add("hidden");
        passwordForm.reset();
        profilePictureUrlForm?.reset();
        profilePictureFileForm?.reset();

        try {
            const security = await loadAccountSecurity();
            renderSettingsPanel(security);
            settingsModal?.classList.remove("hidden");
        } catch (error) {
            console.error(error);
            settingsPasswordError.textContent = "No se pudo cargar la configuración.";
            settingsPasswordError.classList.remove("hidden");
            renderSettingsPanel(currentUser);
            settingsModal?.classList.remove("hidden");
        }
    }

    async function savePasswordFromSettings() {
        settingsPasswordError.classList.add("hidden");
        const saveBtn = document.getElementById("btn-save-password");
        const originalHtml = saveBtn.innerHTML;
        saveBtn.disabled = true;
        saveBtn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Guardando...`;

        const body = {
            newPassword: document.getElementById("settings-new-password").value,
            confirmPassword: document.getElementById("settings-confirm-password").value
        };

        if (!currentPasswordGroup.classList.contains("hidden")) {
            body.currentPassword = document.getElementById("settings-current-password").value;
        }

        try {
            const response = await authFetch("/api/auth/password", {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(body)
            });

            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                settingsPasswordError.textContent =
                    data.message || data.title || data.detail || "No se pudo guardar la contraseña.";
                settingsPasswordError.classList.remove("hidden");
                return;
            }

            if (data.token) {
                saveToken(data.token);
            }
            if (data.user) {
                currentUser = { ...currentUser, ...data.user, isAuthenticated: true, isRegistered: true };
                setUserAvatar(currentUser.nombre, getPictureUrl(currentUser));
                updateUserBadge(currentUser);
            } else {
                await checkSession();
            }

            closeSettingsModal();
        } catch (error) {
            console.error(error);
            settingsPasswordError.textContent = "No se pudo conectar con el servidor.";
            settingsPasswordError.classList.remove("hidden");
        } finally {
            saveBtn.disabled = false;
            saveBtn.innerHTML = originalHtml;
        }
    }

    async function savePictureUrlFromSettings() {
        settingsPictureError?.classList.add("hidden");
        const url = document.getElementById("settings-picture-url")?.value?.trim();
        if (!url) {
            settingsPictureError.textContent = "Ingresa la URL de la imagen.";
            settingsPictureError.classList.remove("hidden");
            return;
        }

        const btn = document.getElementById("btn-save-picture-url");
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Guardando...`;

        try {
            const response = await authFetch("/api/auth/profile-picture", {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ pictureUrl: url })
            });
            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                settingsPictureError.textContent =
                    data.message || data.title || data.detail || "No se pudo guardar la foto.";
                settingsPictureError.classList.remove("hidden");
                return;
            }

            applyProfilePictureResponse(data);
            setSettingsAvatarPreview(currentUser.nombre, getPictureUrl(currentUser));
        } catch (error) {
            console.error(error);
            settingsPictureError.textContent = "No se pudo conectar con el servidor.";
            settingsPictureError.classList.remove("hidden");
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }

    async function uploadPictureFromSettings() {
        settingsPictureError?.classList.add("hidden");
        const file = document.getElementById("settings-picture-file")?.files?.[0];
        if (!file) {
            settingsPictureError.textContent = "Selecciona una imagen.";
            settingsPictureError.classList.remove("hidden");
            return;
        }

        const btn = document.getElementById("btn-save-picture-file");
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Subiendo...`;

        const formData = new FormData();
        formData.append("file", file);

        try {
            const response = await authFetch("/api/auth/profile-picture/upload", {
                method: "POST",
                body: formData
            });
            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                settingsPictureError.textContent =
                    data.message || data.title || data.detail || "No se pudo subir la imagen.";
                settingsPictureError.classList.remove("hidden");
                return;
            }

            applyProfilePictureResponse(data);
            setSettingsAvatarPreview(currentUser.nombre, getPictureUrl(currentUser));
            profilePictureFileForm?.reset();
        } catch (error) {
            console.error(error);
            settingsPictureError.textContent = "No se pudo conectar con el servidor.";
            settingsPictureError.classList.remove("hidden");
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }

    async function removePictureFromSettings() {
        settingsPictureError?.classList.add("hidden");

        try {
            const response = await authFetch("/api/auth/profile-picture", { method: "DELETE" });
            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                settingsPictureError.textContent =
                    data.message || data.title || data.detail || "No se pudo quitar la foto.";
                settingsPictureError.classList.remove("hidden");
                return;
            }

            applyProfilePictureResponse(data);
            setSettingsAvatarPreview(currentUser.nombre, null);
            const pictureUrlInput = document.getElementById("settings-picture-url");
            if (pictureUrlInput) {
                pictureUrlInput.value = "";
            }
            profilePictureFileForm?.reset();
        } catch (error) {
            console.error(error);
            settingsPictureError.textContent = "No se pudo conectar con el servidor.";
            settingsPictureError.classList.remove("hidden");
        }
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
        try {
            const response = await authFetch(`/api/notes/${noteId}`, { method: "DELETE" });
            if (!response.ok) {
                await loadNotes();
                return;
            }

            if (cardEl) {
                cardEl.remove();
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
