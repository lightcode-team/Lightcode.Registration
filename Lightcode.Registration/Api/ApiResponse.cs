using Lightcode.Registration.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Api;

/// <summary>Monta <see cref="ObjectResult"/> e grava JSON no <see cref="HttpContext"/> com o mesmo contrato.</summary>
public static class ApiResponse
{
    public static ObjectResult Success<T>(T data, int statusCode = StatusCodes.Status200OK, string? message = null) =>
        new(new ApiEnvelope<T>
        {
            Error = false,
            Errors = [],
            StatusCode = statusCode,
            Message = message,
            Data = data
        })
        {
            StatusCode = statusCode
        };

    public static ObjectResult Error(int statusCode, params string[] erros) =>
        Error<object>(statusCode, (IEnumerable<string>)erros);

    public static ObjectResult Error(int statusCode, IEnumerable<string> erros) =>
        Error<object>(statusCode, erros);

    public static ObjectResult Error<T>(int statusCode, params string[] erros) =>
        Error<T>(statusCode, (IEnumerable<string>)erros);

    public static ObjectResult Error<T>(int statusCode, IEnumerable<string> erros)
    {
        var lista = NormalizarErros(erros);
        return new ObjectResult(new ApiEnvelope<T?>
        {
            Error = true,
            Errors = lista,
            StatusCode = statusCode,
            Message = null,
            Data = default
        })
        {
            StatusCode = statusCode
        };
    }

    public static Task WriteErrorAsync(HttpContext httpContext, int statusCode, params string[] erros) =>
        WriteErrorAsync(httpContext, statusCode, erros, httpContext.RequestAborted);

    public static async Task WriteErrorAsync(
        HttpContext httpContext,
        int statusCode,
        IEnumerable<string> erros,
        CancellationToken cancellationToken = default)
    {
        var lista = NormalizarErros(erros);
        var body = new ApiEnvelope<object?>
        {
            Error = true,
            Errors = lista,
            StatusCode = statusCode,
            Message = null,
            Data = null
        };

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
    }

    private static IReadOnlyList<string> NormalizarErros(IEnumerable<string> erros)
    {
        var lista = erros
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (lista.Count == 0)
            lista.Add("Ocorreu um erro.");

        return lista;
    }
}
