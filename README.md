# AuthGoogle - Proyecto de Practica de Autenticacion con Google

> [!IMPORTANT]
> **CONFIGURACION DE CREDENCIALES DE GOOGLE (LEER ANTES DE EJECUTAR):**
> Este proyecto contiene marcadores de posicion para las credenciales de Google OAuth en `Api/Program.cs` para evitar la exposicion de llaves de seguridad en repositorios publicos.
> Antes de ejecutar la aplicacion localmente, debe reemplazar los siguientes valores en `Api/Program.cs` (lineas 42 y 43) con sus propias credenciales obtenidas desde la consola de Google Cloud:
> - `options.ClientId = "PONER_AQUI_EL_CLIENT_ID";`
> - `options.ClientSecret = "PONER_AQUI_EL_CLIENT_SECRET";`

## Descripcion General del Proyecto

La aplicacion permite a los usuarios registrarse e iniciar sesion de forma segura utilizando sus cuentas de Google. Una vez autenticados y registrados localmente en la base de datos de PostgreSQL, los usuarios pueden crear, guardar y visualizar sus notas personales en un panel dinamico y responsivo.

## Objetivos del Analisis

- Analizar el flujo de autenticacion OAuth2 de Google y la integracion de cookies de sesion.
- Resolver problemas de hosting y enrutamiento al servir archivos estaticos directamente desde el backend.
- Implementar validacion de identidad (Claims Matching) para evitar ataques de suplantacion de identidad.
- Estructurar el proyecto siguiendo los principios de Clean Architecture.

## Tecnologias y Librerias Utilizadas

### Backend (.NET 10 Web API)
- **Microsoft.AspNetCore.Authentication.Google**: Libreria oficial para manejar el flujo de autenticacion con Google.
- **Microsoft.AspNetCore.Authentication.Cookies**: Middleware para el manejo de sesiones encriptadas basadas en cookies de navegador.
- **Microsoft.EntityFrameworkCore**: ORM para interactuar con la base de datos PostgreSQL.
- **Npgsql.EntityFrameworkCore.PostgreSQL**: Proveedor de EF Core para conectar y mapear entidades en PostgreSQL.

### Frontend (Mismo Origen)
- **HTML5**: Estructuracion semantica de la pagina.
- **CSS3**: Diseño premium responsivo con estetica de modo oscuro y glassmorphism.
- **JavaScript (ES6+)**: Logica cliente reactiva, consumo de APIs locales mediante fetch, validacion de sesiones y prevencion de inyecciones de codigo (XSS).

## Arquitectura de Conexion y Seguridad

### Hosting Unificado (Mismo Origen)
El frontend reside en el directorio `/Client` y es servido directamente por la API de .NET en el puerto `5098`. Esto elimina la necesidad de configurar politicas de CORS en desarrollo y permite que el navegador envie automaticamente las cookies de autenticacion encriptadas en cada llamada de API.

### Mitigacion de Vulnerabilidades de Seguridad

#### Filtro de Autenticacion ([Authorize])
Todos los endpoints sensibles de creacion y lectura de notas estan decorados con el atributo `[Authorize]`, bloqueando accesos no autenticados con un codigo HTTP 401.

#### Validacion de Identidad (Claims Matching)
Para evitar que un usuario autenticado altere los datos del cliente para leer o escribir notas de otra persona, el backend no confia en el parametro de identidad enviado por el cliente. El servidor extrae el Google ID verificado directamente desde los claims firmados de la cookie de autenticacion (`User.FindFirstValue(ClaimTypes.NameIdentifier)`) y lo valida contra el ID solicitado. Si no coinciden, la API responde con un codigo HTTP 403 Forbidden.

## Estructura del Workspace

- **/Api**: Capa de entrada (Controllers, configuracion de Kestrel y middleware).
- **/Application**: Logica de aplicacion, interfaces y servicios.
- **/Domain**: Entidades core del negocio (Usuario, Nota).
- **/Infrastructure**: Implementacion de base de datos, repositorios y configuracion de base de datos.
- **/Client**: Frontend de la aplicacion (index.html, styles.css, app.js).

## Requisitos para Ejecutar Localmente

1. Tener instalado .NET 10 SDK.
2. Contar con un servidor local de PostgreSQL activo y configurar la cadena de conexion en `appsettings.Development.json`.
3. Disponer de credenciales OAuth de Google (Client ID y Client Secret) configuradas en `Program.cs`.
4. Ejecutar el comando `dotnet run` dentro de la carpeta `Api/`.
5. Acceder en el navegador a `http://localhost:5098`.
