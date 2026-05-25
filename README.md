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
> Ese texto **no se configura en el codigo**, sino en Google Cloud Console. Si aparece otro nombre, cambialo asi:
> 1. [Google Cloud Console](https://console.cloud.google.com/) → tu proyecto
> 2. **APIs & Services** → **OAuth consent screen**
> 3. **App name** → `NotesCampus`
> 4. Guarda y vuelve a iniciar sesion en la app

## Descripcion General del Proyecto

La aplicacion permite a los usuarios autenticarse con Google o con credenciales locales, y luego usar un token JWT firmado para autorizar peticiones a la API.

El flujo principal es:
1. El usuario inicia sesion con Google o con correo y contraseña.
2. El backend valida la autenticacion y emite un JWT.
3. El frontend guarda el token en `localStorage`.
4. El frontend envia el token en el encabezado `Authorization: Bearer <token>` en las llamadas protegidas.

## Flujo de Autenticacion con Google

### 1) Inicio de sesion con Google
- Endpoint: `GET /api/auth/login`
- La aplicacion redirige al usuario a Google usando `GoogleDefaults.AuthenticationScheme`.

### 2) Callback de Google
- Endpoint: `GET /api/auth/google-response`
- El backend completa la autenticacion externa usando una cookie temporal (`AuthSchemes.External`).
- Se extraen los claims del usuario: `NameIdentifier`, `Name` y `Email`.

### 3) Generacion del JWT
- Se genera un token JWT con `JwtTokenService`.
- El token contiene:
  - `ClaimTypes.NameIdentifier` => ID del usuario registrado o del usuario de Google en sesion.
  - `ClaimTypes.Name` => Nombre completo.
  - `ClaimTypes.Email` => Correo electronico.
  - `auth_provider` => `google` o `local`.
  - `picture` => URL de la imagen de perfil cuando esta disponible.
- El token se firma con `Jwt:Key` y expira a las 24 horas.

### 4) Retorno al frontend
- El backend redirige al frontend con el token en la URL: `/#token=<token>`.
- El frontend captura el token y lo guarda en `localStorage`.

## Fotos de perfil subidas

Las imagenes que el usuario sube desde Configuracion se guardan en `Api/App_Data/avatars/` (datos locales de ejecucion). Esa carpeta esta en `.gitignore` y **no debe subirse al repositorio**. Las URLs publicas siguen siendo `/avatars/{userId}.ext`.

## Autenticacion Local

El frontend tambien soporta autenticacion local con correo y contraseña.

- `POST /api/auth/register-local` crea una cuenta local y devuelve un JWT.
- `POST /api/auth/login-local` inicia sesion local y devuelve un JWT.

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
- `GET /api/notes`, `POST /api/notes`, `PUT /api/notes/{id}` y `DELETE /api/notes/{id}` requieren JWT valido.
- `POST /api/auth/register-google` tambien requiere JWT valido para completar el registro de un usuario Google.

### Validacion de identidad
- El backend usa `ClaimTypes.NameIdentifier` para identificar al usuario actual.
- Si el claim es un ID interno, se resuelve con la tabla de usuarios.
- Si el claim es un Google ID temporal, se usa para completar el registro de Google.

## Endpoints principales

- `GET /api/auth/login`: inicia el flujo de autenticacion con Google.
- `GET /api/auth/google-response`: recibe el callback de Google y emite el JWT.
- `GET /api/auth/current-user`: devuelve el estado y datos del usuario actual.
- `POST /api/auth/register-local`: registra un usuario local con correo y contraseña.
- `POST /api/auth/login-local`: inicia sesion local con correo y contraseña.
- `POST /api/auth/register-google`: completa el registro de un usuario autenticado por Google.
- `POST /api/auth/logout`: devuelve OK para indicar cierre de sesion en el cliente.
- `GET /api/notes`: obtiene las notas del usuario autenticado.
- `POST /api/notes`: crea una nueva nota para el usuario autenticado.
- `PUT /api/notes/{id}`: actualiza una nota existente.
- `DELETE /api/notes/{id}`: elimina una nota existente.

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
    return token ? { Authorization: `Bearer ${token}` } : {};
}
```

### 3) Envio del token en cada peticion
La funcion `authFetch()` es un wrapper de `fetch()` que añade el header `Authorization`:
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
El frontend llama a los endpoints protegidos con `authFetch()`:
```javascript
const response = await authFetch("/api/notes");
const response = await authFetch("/api/notes", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ titulo: titleVal, contenido: contentVal })
});
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

#presentacion
https://www.canva.com/design/DAHKTV6PkPk/OSw3GJ8bgVJWTo4Kbx60Qw/edit
