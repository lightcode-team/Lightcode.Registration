using System.Text;
using Lightcode.Registration.Api;
using Lightcode.Registration.Application;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Infrastructure;
using Lightcode.Registration.Infrastructure.Persistence.Mongo;
using Lightcode.Registration.AspNetCore.Security;
using Lightcode.Registration.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Lightcode.Registration.AspNetCore.Hosting;

public static class RegistrationApiHostExtensions
{
    public static WebApplicationBuilder AddRegistrationApiHost(
        this WebApplicationBuilder builder,
        Action<RegistrationApiHostOptions>? configureHost = null)
    {
        var hostOptions = new RegistrationApiHostOptions();
        configureHost?.Invoke(hostOptions);

        MongoSerialization.EnsureRegistered();

        builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
        builder.Services.Configure<MasterOptions>(builder.Configuration.GetSection(MasterOptions.SectionName));
        builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
        builder.Services.Configure<TenantDefaultSmtpOptions>(
            builder.Configuration.GetSection(TenantDefaultSmtpOptions.SectionName));
        builder.Services.Configure<RegistrationOptions>(
            builder.Configuration.GetSection(RegistrationOptions.SectionName));

        if (hostOptions.RegisterRabbitMqConnection)
            builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwtSection is null || string.IsNullOrWhiteSpace(jwtSection.SigningKey) || jwtSection.SigningKey.Length < 32)
            throw new InvalidOperationException("Configure Jwt:SigningKey com pelo menos 32 caracteres (use variável de ambiente em produção).");

        builder.Services.AddSingleton<IMongoClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return new MongoClient(cfg.ConnectionString);
        });

        if (hostOptions.RegisterRabbitMqConnection)
        {
            var clientName = hostOptions.RabbitMqConnectionClientName;
            builder.Services.AddSingleton<IConnection>(_ =>
            {
                var o = _.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                var f = new ConnectionFactory
                {
                    HostName = o.HostName,
                    Port = o.Port,
                    UserName = o.UserName,
                    Password = o.Password,
                    VirtualHost = o.VirtualHost,
                    DispatchConsumersAsync = true
                };
                return f.CreateConnection(clientName);
            });
        }

        builder.Services.AddInfrastructure(registerRabbitMqEmailEnqueuePublisher: hostOptions.RegisterRabbitMqConnection);
        builder.Services.AddApplication();

        var corsSection = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
        var allowedOrigins = corsSection?.AllowedOrigins?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins);
                else if (builder.Environment.IsDevelopment())
                    policy.WithOrigins("http://localhost:8080", "http://localhost:5173", "http://localhost:3000");
                else
                    return;

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddScoped<IJwtTenantTokenValidator, JwtTenantTokenValidator>();
        builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, TenantJwtBearerOptionsPostConfigure>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = "role"
                };

                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices
                            .GetRequiredService<IJwtTenantTokenValidator>();
                        await validator.ValidateAsync(context);
                    }
                };
            });

        builder.Services.AddSingleton<IAuthorizationHandler, EmailApiPermissionAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, OAuthClientsPermissionAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, AccountsAdminAuthorizationHandler>();

        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy("HasTenant", p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
            });

            o.AddPolicy(PlatformPolicyNames.PlatformAdmin, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("token_use", "platform_admin");
                p.RequireClaim("platformAdminId");
            });

            o.AddPolicy("TenantAdmin", p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.RequireRole(UserRoles.Admin);
            });

            o.AddPolicy(EmailApiPolicyNames.TemplateRead, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.AddRequirements(new EmailApiPermissionRequirement(EmailApiPermission.TemplateRead));
            });

            o.AddPolicy(EmailApiPolicyNames.TemplateWrite, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.AddRequirements(new EmailApiPermissionRequirement(EmailApiPermission.TemplateWrite));
            });

            o.AddPolicy(EmailApiPolicyNames.SendEmail, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.AddRequirements(new EmailApiPermissionRequirement(EmailApiPermission.SendEmail));
            });

            o.AddPolicy(OAuthClientsPolicyNames.ClientsRead, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.AddRequirements(new OAuthClientsPermissionRequirement(OAuthClientsPermission.ClientsRead));
            });

            o.AddPolicy(OAuthClientsPolicyNames.ClientsWrite, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.AddRequirements(new OAuthClientsPermissionRequirement(OAuthClientsPermission.ClientsWrite));
            });

            o.AddPolicy(AccountsPolicyNames.AccountsAdmin, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("tenantId");
                p.AddRequirements(new AccountsAdminRequirement());
            });
        });

        if (hostOptions.EnableMvcViews)
            builder.Services.AddControllersWithViews();
        else
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

        return builder;
    }

    public static WebApplication UseRegistrationApiPipeline(this WebApplication app)
    {
        app.UseGlobalExceptionHandling();

        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
            app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseCors();
        app.UseAuthentication();
        app.UseTenantResolution();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
