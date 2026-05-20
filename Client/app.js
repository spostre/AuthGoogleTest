document.addEventListener("DOMContentLoaded", () => {
    const TOKEN_KEY = "authToken";

    let currentUser = null;

    const authHeaderAction = document.getElementById("auth-header-action");
    const userInfoSection = document.getElementById("user-info-section");
    const userName = document.getElementById("user-name");
    const userEmail = document.getElementById("user-email");
    const userAvatar = document.getElementById("user-avatar");

    const statusNoticeSection = document.getElementById("status-notice-section");
    const statusMessage = document.getElementById("status-message");
    const statusActions = document.getElementById("status-actions");

    const notesSection = document.getElementById("notes-section");
    const notesGrid = document.getElementById("notes-grid");

    const noteModal = document.getElementById("note-modal");
    const btnShowCreateModal = document.getElementById("btn-show-create-modal");
    const btnCloseModal = document.getElementById("btn-close-modal");
    const btnCancelModal = document.getElementById("btn-cancel-modal");
    const noteForm = document.getElementById("note-form");
    const noteTitle = document.getElementById("note-title");
    const noteContent = document.getElementById("note-content");

    captureTokenFromUrl();
    checkSession();

    function captureTokenFromUrl() {
        const hash = window.location.hash;
        if (hash.startsWith("#token=")) {
            const token = hash.substring(7);
            localStorage.setItem(TOKEN_KEY, token);
            window.history.replaceState(null, "", window.location.pathname);
        }
    }

    function getAuthHeaders() {
        const token = localStorage.getItem(TOKEN_KEY);
        if (!token) return {};
        return { Authorization: `Bearer ${token}` };
    }

    function authFetch(url, options = {}) {
        return fetch(url, {
            ...options,
            headers: {
                ...getAuthHeaders(),
                ...(options.headers || {})
            }
        });
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
        if (!currentUser || !currentUser.isAuthenticated) {
            showGuestUI();
            return;
        }

        userInfoSection.classList.remove("hidden");
        userName.textContent = currentUser.nombre || "Usuario Google";
        userEmail.textContent = currentUser.email || "";
        userAvatar.textContent = (currentUser.nombre || "G").charAt(0).toUpperCase();

        authHeaderAction.innerHTML = `
            <button class="btn btn-danger" id="btn-logout">
                <i class="fa-solid fa-right-from-bracket"></i> Cerrar Sesión
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
        userInfoSection.classList.add("hidden");
        notesSection.classList.add("hidden");

        statusNoticeSection.classList.remove("hidden");
        statusMessage.textContent = "Inicia sesión con Google para acceder a tus notas. Si es tu primera vez, podrás registrarte después de autenticarte.";

        authHeaderAction.innerHTML = ``;

        statusActions.innerHTML = `
            <a href="/api/auth/login" class="btn btn-primary btn-lg">
                <i class="fa-brands fa-google"></i> Iniciar Sesión con Google
            </a>
        `;
    }

    function showUnregisteredUI() {
        notesSection.classList.add("hidden");
        statusNoticeSection.classList.remove("hidden");
        statusMessage.innerHTML = `Hola <strong>${currentUser.nombre}</strong>. Has iniciado sesión con Google, pero aún no estás registrado en nuestra base de datos local de notas.`;

        statusActions.innerHTML = `
            <button class="btn btn-accent btn-lg" id="btn-register-local">
                <i class="fa-solid fa-user-plus"></i> Registrar mi cuenta con Google
            </button>
        `;

        document.getElementById("btn-register-local").addEventListener("click", registerUser);
    }

    async function registerUser() {
        const btn = document.getElementById("btn-register-local");
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Registrando...`;

        try {
            const response = await authFetch("/api/auth/register", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    googleId: currentUser.googleId,
                    nombre: currentUser.nombre,
                    email: currentUser.email
                })
            });

            if (response.ok) {
                await checkSession();
            } else {
                alert("Error al registrar el usuario en el backend.");
                btn.disabled = false;
                btn.innerHTML = `<i class="fa-solid fa-user-plus"></i> Registrar mi cuenta con Google`;
            }
        } catch (error) {
            console.error("Error al registrar usuario:", error);
            alert("Ocurrió un error al conectar con la API.");
            btn.disabled = false;
            btn.innerHTML = `<i class="fa-solid fa-user-plus"></i> Registrar mi cuenta con Google`;
        }
    }

    function showRegisteredUI() {
        statusNoticeSection.classList.add("hidden");
        notesSection.classList.remove("hidden");
        loadNotes();
    }

    async function loadNotes() {
        notesGrid.innerHTML = `
            <div class="notice-card" style="grid-column: 1/-1; padding: 2rem; border: none; background: transparent;">
                <i class="fa-solid fa-circle-notch fa-spin" style="font-size: 2rem; color: var(--accent-cyan);"></i>
                <p style="margin-top: 1rem;">Cargando tus notas...</p>
            </div>
        `;

        try {
            const response = await authFetch(`/api/notes/${currentUser.googleId}`);
            if (!response.ok) throw new Error("No se pudieron cargar las notas.");

            const notes = await response.json();
            renderNotes(notes);
        } catch (error) {
            console.error("Error al cargar notas:", error);
            notesGrid.innerHTML = `
                <div class="notice-card" style="grid-column: 1/-1; border-color: rgba(239,68,68,0.2);">
                    <i class="fa-solid fa-circle-exclamation" style="font-size: 2.5rem; color: var(--accent-red);"></i>
                    <p style="margin-top: 1rem; color: var(--accent-red);">Error al conectar con el servidor.</p>
                </div>
            `;
        }
    }

    function renderNotes(notes) {
        notesGrid.innerHTML = "";

        if (notes.length === 0) {
            notesGrid.innerHTML = `
                <div class="notice-card" style="grid-column: 1/-1; padding: 3rem 1.5rem;">
                    <i class="fa-regular fa-note-sticky" style="font-size: 3rem; color: var(--text-secondary); opacity: 0.5; margin-bottom: 1rem;"></i>
                    <h3>No tienes notas guardadas</h3>
                    <p style="font-size: 0.95rem; margin-top: 0.5rem;">¡Presiona "Nueva Nota" arriba para comenzar!</p>
                </div>
            `;
            return;
        }

        notes.forEach(note => {
            const formattedDate = new Date(note.fechaCreacion).toLocaleDateString("es-ES", {
                day: "numeric",
                month: "short",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            });

            const card = document.createElement("div");
            card.className = "note-card";
            card.innerHTML = `
                <div>
                    <div class="note-header-card">
                        <h3>${escapeHTML(note.titulo)}</h3>
                    </div>
                    <p class="note-body-card">${escapeHTML(note.contenido).replace(/\n/g, "<br>")}</p>
                </div>
                <div class="note-footer-card">
                    <span class="note-date">
                        <i class="fa-regular fa-calendar"></i> ${formattedDate}
                    </span>
                </div>
            `;
            notesGrid.appendChild(card);
        });
    }

    btnShowCreateModal.addEventListener("click", () => {
        noteModal.classList.remove("hidden");
        noteTitle.focus();
    });

    function hideModal() {
        noteModal.classList.add("hidden");
        noteForm.reset();
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

        const submitBtn = noteForm.querySelector("button[type='submit']");
        const originalHtml = submitBtn.innerHTML;
        submitBtn.disabled = true;
        submitBtn.innerHTML = `<i class="fa-solid fa-spinner fa-spin"></i> Guardando...`;

        try {
            const response = await authFetch("/api/notes", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    googleId: currentUser.googleId,
                    titulo: titleVal,
                    contenido: contentVal
                })
            });

            if (response.ok) {
                hideModal();
                await loadNotes();
            } else {
                alert("Error al guardar la nota en el servidor.");
            }
        } catch (error) {
            console.error("Error al crear nota:", error);
            alert("Ocurrió un error al guardar la nota.");
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalHtml;
        }
    });

    function escapeHTML(str) {
        return str.replace(/[&<>'"]/g,
            tag => ({
                "&": "&amp;",
                "<": "&lt;",
                ">": "&gt;",
                "'": "&#39;",
                '"': "&quot;"
            }[tag] || tag)
        );
    }
});
