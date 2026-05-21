using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Auth;
using Api.Services;
using Application;
using Infrastructure;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var clientPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Client"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = clientPath
});

Console.WriteLine($"[DIAGNÓSTICO] ContentRootPath: {builder.Environment.ContentRootPath}");
Console.WriteLine($"[DIAGNÓSTICO] WebRootPath: {builder.Environment.WebRootPath}");
Console.WriteLine($"[DIAGNÓSTICO] ¿Existe la carpeta Client?: {Directory.Exists(builder.Environment.WebRootPath)}");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<JwtTokenService>();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var jwtKey = builder.Configuration["Jwt:Key"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
{
    throw new InvalidOperationException(
        "Configura Authentication:Google:ClientId y ClientSecret en appsettings.Development.json, " +
        "variables de entorno (Authentication__Google__ClientId / ClientSecret).");
}

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Configura Jwt:Key en appsettings.Development.json o en la variable de entorno Jwt__Key.");
}

if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("Password=;", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "Configura ConnectionStrings:DefaultConnection con la contraseña de PostgreSQL en appsettings.Development.json " +
        "o en la variable de entorno ConnectionStrings__DefaultConnection.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = AuthSchemes.External;
})
.AddCookie(AuthSchemes.External, options =>
{
    options.Cookie.Name = "AuthGoogle.OAuth";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    options.SlidingExpiration = false;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
})
.AddGoogle(options =>
{
    options.ClientId = googleClientId;
    options.ClientSecret = googleClientSecret;
    options.SignInScheme = AuthSchemes.External;
    options.SaveTokens = true;
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Events.OnCreatingTicket = async context =>
    {
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        if (!string.IsNullOrEmpty(identity.FindFirst("picture")?.Value))
        {
            return;
        }

        var accessToken = context.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
        {
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: context.HttpContext.RequestAborted);

        if (document.RootElement.TryGetProperty("picture", out var pictureElement))
        {
            var pictureUrl = pictureElement.GetString();
            if (!string.IsNullOrEmpty(pictureUrl))
            {
                identity.AddClaim(new Claim("picture", pictureUrl));
            }
        }
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    try
    {
        context.Database.ExecuteSqlRaw("""
            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "PasswordHash" character varying(500);
            ALTER TABLE "Usuarios" ALTER COLUMN "GoogleId" DROP NOT NULL;
            """);
    }
    catch
    {
        // La base ya puede estar actualizada o usar otro motor.
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
