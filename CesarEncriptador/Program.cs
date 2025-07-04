using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CesarEncriptador.Services;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Cesar Encriptador API", 
        Version = "v1",
        Description = "API para cifrado y descifrado usando el algoritmo de César con autenticación JWT"
    });
    
    // Configurar para mostrar formularios en lugar de JSON
    c.CustomSchemaIds(type => type.Name);
    
    // Configurar Swagger para JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Configurar JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes("TuClaveSecretaSuperSegura123!@#$%^&*()")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Registrar servicios
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<CesarService>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Habilitar Swagger en todos los entornos
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cesar Encriptador API v1");
    c.RoutePrefix = string.Empty; // Hacer que Swagger esté disponible en la raíz
    c.DocumentTitle = "Cesar Encriptador API";
    c.DefaultModelsExpandDepth(-1); // Ocultar modelos por defecto
});

app.UseHttpsRedirection();

// Servir archivos estáticos
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Configurar Forwarded Headers para obtener la IP real detrás de proxies
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { },
    KnownProxies = { }
});

app.Use(async (context, next) =>
{
    string allowedIp = "187.155.101.200";
    string? remoteIpString = context.Connection.RemoteIpAddress?.ToString();
    string? xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

    // Analizar todas las IPs posibles
    var ipsToCheck = new List<string>();
    if (!string.IsNullOrEmpty(xForwardedFor))
        ipsToCheck.AddRange(xForwardedFor.Split(',').Select(ip => ip.Trim()));
    if (!string.IsNullOrEmpty(remoteIpString))
        ipsToCheck.Add(remoteIpString);

    // Para depuración, mostrar todas las IPs analizadas
    context.Response.Headers.Add("X-Debug-IPs-Checked", string.Join("|", ipsToCheck));
    context.Response.Headers.Add("X-Debug-RemoteIp", remoteIpString ?? "null");
    context.Response.Headers.Add("X-Debug-XForwardedFor", xForwardedFor ?? "null");

    // Permitir solo si alguna IP coincide exactamente con la IP permitida
    bool ipPermitida = ipsToCheck.Any(ip => ip == allowedIp || ip == $"::ffff:{allowedIp}");
    if (!ipPermitida)
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync($"Acceso denegado: solo se permite la IP autorizada. IPs analizadas: {string.Join(", ", ipsToCheck)}");
        return;
    }
    await next();
});

app.MapControllers();

app.Run();
