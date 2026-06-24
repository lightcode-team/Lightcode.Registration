using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Controllers
{
    public abstract class BaseController : Controller
    {
        // protected readonly ILogger Logger;

        protected BaseController()
        {
            // Logger = logger;
        }

        protected string? GetHeader(string name)
        {
            if (!Request.Headers.TryGetValue(name, out var values))
                return null;

            var value = values.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        protected string? GetValueQueryOrHeader(string name)
        {
            if (Request.Headers.TryGetValue(name, out var headerValues))
            {
                var headerValue = headerValues.FirstOrDefault()?.Trim();

                if (!string.IsNullOrWhiteSpace(headerValue))
                    return headerValue;
            }

            if (Request.Query.TryGetValue(name, out var queryValues))
            {
                var queryValue = queryValues.FirstOrDefault()?.Trim();

                if (!string.IsNullOrWhiteSpace(queryValue))
                    return queryValue;
            }

            return null;
        }
    }
}
