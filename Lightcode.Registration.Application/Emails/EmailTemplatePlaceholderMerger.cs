namespace Lightcode.Registration.Application.Emails;

public static class EmailTemplatePlaceholderMerger
{
    /// <summary>Substitui ocorrências de <c>{{chave}}</c> (comparação sem distinção de maiúsculas).</summary>
    public static string Merge(string template, IReadOnlyDictionary<string, string>? parameters)
    {
        if (string.IsNullOrEmpty(template) || parameters is null || parameters.Count == 0)
            return template;

        var result = template;
        foreach (var kv in parameters)
        {
            var token = "{{" + kv.Key + "}}";
            result = result.Replace(token, kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
