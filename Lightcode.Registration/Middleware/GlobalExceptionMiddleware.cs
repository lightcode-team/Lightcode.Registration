using Lightcode.Registration.Api;
using Microsoft.AspNetCore.Http;

namespace Lightcode.Registration.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Exceção após o início da resposta; não é possível padronizar o retorno.");
                throw;
            }

            logger.LogError(ex, "Exceção não tratada na requisição.");

            var erros = new List<string> { "Erro interno não esperado." };
            if (environment.IsDevelopment())
                erros.Add(ex.Message);

            await ApiResponse.WriteErrorAsync(context, StatusCodes.Status500InternalServerError, erros, context.RequestAborted);
        }
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();
}
