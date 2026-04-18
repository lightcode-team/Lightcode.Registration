using System.Text;
using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Infrastructure;
using Lightcode.Registration.Infrastructure.Persistence.Mongo;
using Lightcode.Registration.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

MongoSerialization.EnsureRegistered();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<MasterOptions>(builder.Configuration.GetSection(MasterOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (jwtSection is null || string.IsNullOrWhiteSpace(jwtSection.SigningKey) || jwtSection.SigningKey.Length < 32)
    throw new InvalidOperationException("Configure Jwt:SigningKey com pelo menos 32 caracteres (use variável de ambiente em produção).");

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return new MongoClient(cfg.ConnectionString);
});

builder.Services.AddInfrastructure();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSection.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSection.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("HasTenant", p =>
    {
        p.RequireAuthenticatedUser();
        p.RequireClaim("tenantId");
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var erros = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors.Select(err =>
                string.IsNullOrWhiteSpace(err.ErrorMessage)
                    ? $"Campo inválido: {e.Key}"
                    : err.ErrorMessage))
            .ToList();

        return ApiResponse.Error(StatusCodes.Status400BadRequest, erros);
    };
});

var app = builder.Build();

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapControllers();

app.Run();
