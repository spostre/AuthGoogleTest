# Guia de Arquitectura: Google Authentication en Clean Architecture

Este documento explica de manera detallada como se integra el flujo de autenticacion de Google (OAuth2) en este proyecto, respondiendo a la pregunta de si solo reside en Program.cs o si afecta a otros modulos y capas de la arquitectura.

---

## 1. La Filosofia de Clean Architecture y la Autenticacion

En el diseño de software basado en Clean Architecture (Arquitectura Limpia), la regla fundamental es la de Independencia de Frameworks y Proveedores Externos. El nucleo del negocio (las reglas de tu aplicacion) no debe depender de como el usuario demuestra quien es.

Por lo tanto, en este proyecto:
- Las capas internas (Domain y Application) son completamente agnósticas a Google. No contienen referencias a librerias de Google, ni metodos de inicio de sesion de terceros.
- Las capas externas (Api y Presentation/Frontend) se encargan de interactuar con Google y empaquetar esa identidad en un formato estandar que el nucleo del negocio si pueda entender (un simple string con el Google ID).

---

## 2. Mapa de Google Authentication en los Diferentes Modulos

El flujo de autenticacion de Google no esta "en un solo lugar", sino que se divide en diferentes responsabilidades a lo largo de las capas externas del proyecto.

### Capa 1: Api - Program.cs (El Registro y Middleware)
Aqui es donde se "enciende" y configura el motor de autenticacion de ASP.NET Core. 

1. **Configuracion de Servicios:** Se le indica al contenedor de dependencias que utilizaremos autenticacion basada en Cookies encriptadas locales y el esquema externo de Google.
   ```csharp
   builder.Services.AddAuthentication(options => 
   { 
       options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; 
       options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme; 
   }) 
   .AddCookie() 
   .AddGoogle(options => 
   { 
       options.ClientId = "TU_CLIENT_ID"; 
       options.ClientSecret = "TU_CLIENT_SECRET"; 
   });
   ```
2. **Activacion en el Pipeline de HTTP:** Se le indica al servidor Kestrel que analice la identidad de cada peticion entrante antes de mandarla a los controladores.
   ```csharp
   app.UseAuthentication(); // ¿Quien eres? (Valida la cookie)
   app.UseAuthorization();  // ¿A que tienes permiso? (Filtra accesos)
   ```

### Capa 2: Api - AuthController.cs (El Coordinador del Flujo)
Este controlador actua como el puente directo entre el cliente (Frontend), el middleware de ASP.NET y los servidores de Google. Tiene los siguientes endpoints clave:

- **`/api/auth/login` (Login):** Lanza el "Challenge" de Google. Redirige al navegador del usuario hacia la pagina oficial de inicio de sesion de Google.
- **`/api/auth/google-response` (Callback):** Es la URL de retorno registrada en Google. Cuando el usuario inicia sesion con exito en Google, este endpoint recibe la confirmacion, extrae la informacion basica (Nombre, Email, Google ID) y crea una Cookie de Sesion encriptada en el navegador del usuario para mantener la sesion abierta.
- **`/api/auth/current-user` (Verificacion de Estado):** El frontend llama a este endpoint al cargar la pagina para saber si el usuario tiene una sesion activa y obtener sus datos basicos.
- **`/api/auth/logout` (Cierre de Sesion):** Elimina la cookie de sesion local de ASP.NET Core.

### Capa 3: Api - Controllers/NotesController.cs (El Consumidor de Seguridad)
El controlador de notas no configura nada de Google, pero se beneficia directamente de ello gracias a dos herramientas:

1. **El atributo `[Authorize]`:** Bloquea el acceso a cualquier peticion que no tenga la cookie de sesion valida creada por Google Auth.
2. **Claims Principal (`User`):** Cuando el middleware valida la cookie de Google, inyecta la informacion del usuario en el objeto `User` del controlador. Esto permite extraer el Google ID real de forma encriptada para validar la identidad de quien realiza la consulta:
   ```csharp
   var authenticatedGoogleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
   ```

### Capa 4: Domain (El Core del Negocio - Desacoplado)
En tu entidad `Usuario.cs` dentro del modulo `Domain/Entities/`, solo existe esta propiedad:
```csharp
public string GoogleId { get; set; } = string.Empty;
```
**Analisis:** Nota que el Dominio no sabe que es Google, ni como funciona su API de OAuth. Solo almacena un texto unico (`GoogleId`) que sirve como identificador unico del usuario. Esto permite que si en el futuro decides cambiar Google por Facebook, GitHub o un inicio de sesion tradicional con Email y Contraseña, el modulo `Domain` y la base de datos no sufriran ningun cambio estructural.

### Capa 5: Client/app.js (El Frontend)
El frontend de JavaScript no interactua con el API de Google directamente. En su lugar, interactua de forma transparente con los endpoints de tu API local:
1. Redirige la pantalla a `/api/auth/login` para iniciar el inicio de sesion.
2. Cada peticion que realiza usando `fetch()` incluye automaticamente la cookie de sesion (`credentials: 'include'`), la cual es procesada por el middleware que configuramos en `Program.cs`.

---

## 3. Resumen del Flujo de Ejecucion

1. El usuario hace clic en "Iniciar sesion con Google" en el Frontend.
2. El navegador va a `/api/auth/login` (`AuthController`).
3. El middleware configurado en `Program.cs` redirige al usuario a los servidores de Google.
4. El usuario pone sus datos en la pagina segura de Google.
5. Google redirige al usuario de vuelta a `/api/auth/google-response` con un codigo de verificacion.
6. El middleware de ASP.NET Core intercambia ese codigo por los datos del usuario tras bambalinas y crea la Cookie de sesion.
7. El endpoint redirige al usuario al Frontend (`/`).
8. El Frontend ahora puede llamar a endpoints seguros (como crear notas en `NotesController`), enviando la cookie de sesion de forma automatica y segura en cada llamada de API.
