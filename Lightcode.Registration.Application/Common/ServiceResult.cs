namespace Lightcode.Registration.Application.Common;

/// <summary>Resultado de caso de uso na camada de aplicação (sem dependência de ASP.NET Core).</summary>
public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static ServiceResult<T> Ok(T value, int statusCode = 200) =>
        new()
        {
            IsSuccess = true,
            Value = value,
            StatusCode = statusCode,
            Errors = []
        };

    public static ServiceResult<T> Fail(int statusCode, params string[] errors) =>
        Fail(statusCode, (IEnumerable<string>)errors);

    public static ServiceResult<T> Fail(int statusCode, IEnumerable<string> errors)
    {
        var list = errors.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToList();
        if (list.Count == 0)
            list.Add("Operação não pôde ser concluída.");

        return new ServiceResult<T>
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Errors = list
        };
    }
}
