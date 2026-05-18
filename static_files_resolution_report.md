# Reporte Técnico: Diagnóstico y Resolución del Servidor de Archivos Estáticos en .NET

Este documento detalla el proceso sistemático de análisis, depuración y resolución del error **HTTP 404** al servir archivos estáticos desde una carpeta personalizada (`Client/`) en un proyecto ASP.NET Core.

---

## 🔍 Resumen del Problema
Al arrancar el servidor web Kestrel a través de `dotnet run`, las solicitudes a la raíz del servidor (`http://localhost:5098/`) devolvían un código de error **HTTP 404 (Not Found)** en el navegador web, en lugar de servir el archivo `index.html` del frontend.

Durante el proceso de depuración, nos enfrentamos a **dos barreras técnicas consecutivas** impuestas por el núcleo de seguridad y diseño de ASP.NET Core:

---

## 🛠️ Fase 1: Barrera de Seguridad de Cadenas Relativas (`..`)

### 1. El Error Inicial
Intentamos configurar el directorio raíz del frontend utilizando el siguiente fragmento en `Program.cs`:
```csharp
builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "..", "Client");
```

### 2. El Diagnóstico
En la teoría tradicional de C#, esta combinación de rutas es perfectamente lógica: sube un nivel (`..`) desde el directorio del proyecto `Api/` y entra en la carpeta `Client/`.

Sin embargo, **Kestrel y su motor de archivos estáticos (`PhysicalFileProvider`) tienen una regla estricta de seguridad contra Directory Traversal (Ataques de Salto de Directorio)**:
* Si Kestrel detecta que la ruta del `WebRootPath` contiene segmentos relativos explícitos (como `..` o `.`), **rechaza de forma silenciosa la carpeta** por considerarla un riesgo de seguridad de acceso al disco.
* Al rechazarla, el middleware `app.UseStaticFiles()` no puede encontrar los archivos y devuelve un **HTTP 404**.

### 3. La Solución Parcial
Para resolver esto, forzamos al sistema operativo a resolver la ruta física absoluta antes de entregársela al motor de .NET usando `Path.GetFullPath`:
```csharp
builder.Environment.WebRootPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Client"));
// Resultado: "E:\Users\...\Client\" (Sin segmentos relativos "..")
```

---

## 🔌 Fase 2: Barrera de Tiempos de Instanciación en .NET

### 1. El Segundo Error
A pesar de limpiar la ruta, el servidor seguía arrojando un **HTTP 404** al visitar la página.

### 2. El Diagnóstico (.NET Core Internals)
Investigando la arquitectura de inicialización de hospedaje en .NET se descubrió un cambio crucial en el comportamiento del framework:
* Al llamar a `var builder = WebApplication.CreateBuilder(args);`, el motor de .NET **inicializa inmediatamente** todos sus servicios internos, incluyendo el proveedor de archivos físicos por defecto apuntando a la carpeta estándar `wwwroot`.
* Asignar `builder.Environment.WebRootPath = ...` en las líneas siguientes modifica la propiedad en memoria del builder, pero **no actualiza los servicios internos ni el FileProvider que el middleware ya instanció** con el valor original por defecto.
* Kestrel seguía buscando la carpeta `wwwroot` (que no existía) e ignorando la carpeta `Client` silenciosamente.

### 3. La Solución Definitiva
Para solucionar el orden de inicialización, debemos indicarle a .NET la ruta personalizada del `WebRootPath` **antes y durante** la construcción misma del Host. Esto se logra pasando una configuración del tipo `WebApplicationOptions` al método de creación del Builder:

```csharp
// 1. Resolver la ruta absoluta de la carpeta Client desde el inicio
var clientPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Client"));

// 2. Pasar el WebRootPath dentro de las opciones de instanciación del constructor
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = clientPath // <- Se registra aquí en tiempo de instanciación
});
```

Al hacerlo de esta forma, .NET inicializa correctamente todos los proveedores de servicios y middleware apuntando a la carpeta física `Client/` desde el primer milisegundo de ejecución del ciclo de vida del servidor.

---

## 📈 Código Final Implementado en `Program.cs`

A continuación se muestra cómo quedó configurado el pipeline en la cabecera de **[Program.cs](file:///e:/Users/moggamex/Documents/moggamex/todo/learn/campuslands/Net/practica/AuthGoogle/Api/Program.cs)**:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Infrastructure;
using Infrastructure.Data;
using Application;

// 1. Configurar y resolver el Web Root Path de forma absoluta
var clientPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Client"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = clientPath
});

// Registrar los servicios estándar del contenedor...
```

---

## 🎓 Lecciones Aprendidas

1. **La seguridad es silenciosa:** Las librerías de infraestructura (como Kestrel o Entity Framework) suelen fallar de forma silenciosa arrojando códigos genéricos (como 404) cuando se violan sus políticas de seguridad locales (ej. Directory Traversal).
2. **El ciclo de vida importa:** En frameworks maduros como .NET, el orden y momento de la configuración es crítico; configurar propiedades después del constructor (`Build` o `CreateBuilder`) puede no tener efecto si los servicios internos ya se han registrado con los valores por defecto.
