using Lightcode.Registration.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Api;

public static class ServiceResultHttpExtensions
{
    public static IActionResult ToApiResponse<T>(this ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return ApiResponse.Success(result.Value!, result.StatusCode);

        return ApiResponse.Error(result.StatusCode, result.Errors);
    }
}
