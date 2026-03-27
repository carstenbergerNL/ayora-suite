using System.Net;
using System.Data.Common;
using Ayora.Shared.Contracts.Api;
using Ayora.Shared.Errors;
using Serilog;

namespace Ayora.Api.Middleware;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            Log.Warning(ex, "Handled application error {Code}", ex.Code);
            await WriteAsync(context, ex.StatusCode, ApiResponse<object>.Fail(ex.Code, ex.Message));
        }
        catch (DbException ex)
        {
            Log.Error(ex, "Database exception");
            await WriteAsync(context, (int)HttpStatusCode.ServiceUnavailable,
                ApiResponse<object>.Fail("db.unavailable", "Database is unavailable."));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception");
            await WriteAsync(context, (int)HttpStatusCode.InternalServerError,
                ApiResponse<object>.Fail("server.error", "An unexpected error occurred."));
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, ApiResponse<object> response)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }
}

