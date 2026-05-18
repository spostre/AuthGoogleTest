using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Infrastructure;
using Infrastructure.Data;
using Application;

var clientPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Client"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = clientPath
});

Console.WriteLine($"[DIAGNÓSTICO] ContentRootPath: {builder.Environment.ContentRootPath}");
Console.WriteLine($"[DIAGNÓSTICO] WebRootPath: {builder.Environment.WebRootPath}");
Console.WriteLine($"[DIAGNÓSTICO] ¿Existe la carpeta Client?: {Directory.Exists(builder.Environment.WebRootPath)}");


// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Registrar las capas de aplicación e infraestructura
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options => 
{ 
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; 
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme; 
}) 
.AddCookie() 
.AddGoogle(options => 
{ 
    options.ClientId = "PONER_AQUI_EL_CLIENT_ID"; 
    options.ClientSecret = "PONER_AQUI_EL_CLIENT_SECRET"; 
});

var app = builder.Build();

// Crear la base de datos y aplicar las tablas si no existe
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
