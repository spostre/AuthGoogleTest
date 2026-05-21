# AuthGoogle - Proyecto de Practica de Autenticacion con Google y JWT

> [!IMPORTANT]
> **CONFIGURACION DE SECRETOS (LEER ANTES DE EJECUTAR):**
> No coloques credenciales en el codigo fuente. Copia `Api/appsettings.Development.example.json` a `Api/appsettings.Development.json` (este archivo esta en `.gitignore`) y completa:
> - `Authentication:Google:ClientId` y `Authentication:Google:ClientSecret` (Google Cloud Console)
> - `Jwt:Key` (minimo 32 caracteres)
> - `Jwt:Issuer` y `Jwt:Audience`
> - `ConnectionStrings:DefaultConnection` (incluye la contraseña de PostgreSQL)
>
> Tambien puedes usar variables de entorno equivalentes:
> - `Authentication__Google__ClientId`
> - `Authentication__Google__ClientSecret`
> - `Jwt__Key`
> - `ConnectionStrings__DefaultConnection`
>
> Si alguna credencial llego a subirse al repositorio, **regenerala de inmediato** en Google Cloud y en PostgreSQL.

> [!IMPORTANT]
> **NOMBRE EN LA PANTALLA DE GOOGLE ("Sign in to ..."):**
> Ese texto **no se configura en el codigo**, sino en Google Cloud Console. Si aparece otro nombre (por ejemplo `n8n super test`), cambialo asi:
> 1. [Google Cloud Console](https://console.cloud.google.com/) → tu proyecto
> 2. **APIs & Services** → **OAuth consent screen**
> 3. **App name** → `NotesCampus` (o el nombre que quieras mostrar)
> 4. Guarda y vuelve a iniciar sesion en la app

## Descripcion General del Proyecto

La aplicacion permite a los usuarios autenticarse con Google y, a continuacion, obtener un token JWT firmado que se utiliza para autorizar peticiones a la API.

El flujo principal es:
1. El usuario inicia inicio de sesion con Google.
2. Google valida la cuenta y emite una autorizacion externa.
3. El backend recibe ese callback temporal, genera un JWT y lo devuelve al frontend.
4. El frontend almacena el token y lo envia en el encabezado `Authorization: Bearer <token>` en las llamadas protegidas.

## Flujo de Autenticacion JWT

### 1) Inicio de sesion con Google
- Endpoint: `GET /api/auth/login`
- La aplicacion redirige al usuario a Google usando `GoogleDefaults.AuthenticationScheme`.
- Google responde al callback configurado en el servidor.

### 2) Callback temporal de Google
- Endpoint: `GET /api/auth/google-response`
- El backend completa la autenticacion externa usando un cookie temporal (`AuthSchemes.External`).
- Se extraen los claims del usuario Google: `NameIdentifier`, `Name` y `Email`.

### 3) Generacion del JWT
- Se genera un token JWT con `JwtTokenService.GenerateToken(...)`.
- El token contiene:
  - `ClaimTypes.NameIdentifier` => Google ID
  - `ClaimTypes.Name` => Nombre completo del usuario
  - `ClaimTypes.Email` => Correo electronico
- El token es firmado con la clave `Jwt:Key` y valido durante 24 horas.

### 4) Retorno al frontend
- El backend cierra la sesion temporal de autenticacion externa.
- Redirige a la aplicacion cliente con el token en la URL: `/#token=<token>`.
- El frontend debe capturar este token y usarlo en futuras peticiones.

## Autenticacion y Autorizacion en el Backend

### Configuracion principal
- El backend usa JWT Bearer como esquema de autenticacion por defecto (`JwtBearerDefaults.AuthenticationScheme`).
- El esquema de Google se configura con `SignInScheme = AuthSchemes.External` para usar una cookie temporal solo durante el callback.
- El servidor valida:
  - `issuer`
  - `audience`
  - `lifetime`
  - `signature`

### Endpoints protegidos
- `POST /api/auth/register` requiere `[Authorize]`.
- Cualquier otro endpoint con `[Authorize]` en el backend tambien exige un JWT valido.

### Validacion de identidad
- El registro y operaciones sensibles usan el claim `ClaimTypes.NameIdentifier` extraido del JWT.
- El backend compara siempre el Google ID autenticado con el Google ID enviado desde el cliente.
- Si los valores no coinciden, se devuelve `403 Forbidden`.

## Endpoints principales

- `GET /api/auth/login`: inicia el flujo de autenticacion con Google.
- `GET /api/auth/google-response`: recibe el callback de Google y emite el JWT.
- `GET /api/auth/current-user`: devuelve el estado de autenticacion del usuario actual.
- `POST /api/auth/register`: registra el usuario localmente en la base de datos, requiere JWT valido.
- `POST /api/auth/logout`: cierra sesion en el cliente, instructivo para eliminar el token local.

## Uso del Token JWT en el Frontend

El archivo `Client/app.js` contiene el flujo completo de manejo del token JWT:

### 1) Captura del token desde la URL
Cuando el backend redirige a `/#token=<token>`, el frontend lo captura:
```javascript
function captureTokenFromUrl() {
    const hash = window.location.hash;
    if (hash.startsWith("#token=")) {
        const token = hash.substring(7);
        localStorage.setItem(TOKEN_KEY, token);
        window.history.replaceState(null, "", window.location.pathname);
    }
}
```

### 2) Preparacion del header Authorization
Una funcion auxiliar obtiene el token de localStorage y lo prepara para ser enviado:
```javascript
function getAuthHeaders() {
    const token = localStorage.getItem(TOKEN_KEY);
    if (!token) return {};
    return { Authorization: `Bearer ${token}` };
}
```

### 3) Envio del token en cada peticion
La funcion `authFetch()` es un wrapper alrededor de `fetch()` que automaticamente añade el header `Authorization`:
```javascript
function authFetch(url, options = {}) {
    return fetch(url, {
        ...options,
        headers: {
            ...getAuthHeaders(),
            ...(options.headers || {})
        }
    });
}
```

### 4) Uso en peticiones a la API
Todos los endpoints protegidos se llaman mediante `authFetch()`:
```javascript
// Registrar usuario
const response = await authFetch("/api/auth/register", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
        googleId: currentUser.googleId,
        nombre: currentUser.nombre,
        email: currentUser.email
    })
});

// Cargar notas
const response = await authFetch(`/api/notes/${currentUser.googleId}`);

// Crear una nota
const response = await authFetch("/api/notes", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
        googleId: currentUser.googleId,
        titulo: titleVal,
        contenido: contentVal
    })
});
```

### 5) Manejo de sesion expirada
Si el backend responde con `401 Unauthorized`, el token ha expirado y se elimina de localStorage:
```javascript
const response = await authFetch("/api/auth/current-user");
if (response.status === 401) {
    localStorage.removeItem(TOKEN_KEY);
    showGuestUI();
    return;
}
```

## Tecnologias y Librerias Utilizadas

### Backend (.NET 10 Web API)
- `Microsoft.AspNetCore.Authentication.Google`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Authentication.Cookies`
- `Microsoft.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`

### Frontend (Mismo Origen)
- `HTML5`, `CSS3`, `JavaScript (ES6+)`

## Arquitectura de Conexion y Seguridad

### Hosting Unificado (Mismo Origen)
El frontend reside en `/Client` y es servido directamente por la API de .NET. Esto simplifica el despliegue local y evita configuraciones adicionales de CORS en desarrollo.

### Politicas de seguridad
- No se confia en la identidad enviada por el cliente.
- El backend extrae los claims firmados del JWT para todas las operaciones autorizadas.
- El cookie temporal `AuthGoogle.OAuth` se usa solo durante el callback de Google y expira rapido.

## Estructura del Workspace

- `/Api`: API, controladores, configuracion de autenticacion y servicios.
- `/Application`: logica de negocio, servicios e interfaces.
- `/Domain`: entidades del dominio (`Usuario`, `Nota`).
- `/Infrastructure`: infraestructura de datos y EF Core.
- `/Client`: frontend estatico.

## Requisitos para Ejecutar Localmente

<<<<<<< HEAD
1. Tener instalado .NET 10 SDK.
2. Contar con un servidor local de PostgreSQL activo y configurar la cadena de conexion en `appsettings.Development.json`.
3. Disponer de credenciales OAuth de Google configuradas en `appsettings.Development.json` (ver plantilla `appsettings.Development.example.json`).
4. Ejecutar el comando `dotnet run` dentro de la carpeta `Api/`.
5. Acceder en el navegador a `http://localhost:5098`.

### Acceso por tunel Cloudflare (trycloudflare.com)

Si expones la API con `cloudflared tunnel`, configura en `Api/appsettings.Development.json`:

```json
"App": {
  "PublicOrigin": "https://draws-catalyst-rear-cultural.trycloudflare.com"
}
```

En [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services** → **Credentials** → tu cliente OAuth → **Authorized redirect URIs**, agrega:

`https://draws-catalyst-rear-cultural.trycloudflare.com/signin-google`

(Reemplaza el host si tu URL de tunel cambia.) Luego abre la app por esa URL, no por `localhost`.
=======
1. Instalar .NET 10 SDK.
2. Tener PostgreSQL corriendo localmente.
3. Copiar `Api/appsettings.Development.example.json` a `Api/appsettings.Development.json`.
4. Configurar:
   - `Authentication:Google:ClientId`
   - `Authentication:Google:ClientSecret`
   - `Jwt:Key` (minimo 32 caracteres)
   - `Jwt:Issuer`
   - `Jwt:Audience`
   - `ConnectionStrings:DefaultConnection`
5. Ejecutar `dotnet run` desde la carpeta `Api`.
6. Abrir `http://localhost:5098` en el navegador.
>>>>>>> b700a0b621ad0644e2886f6c5047da3136eaad77
