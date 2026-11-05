using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.Facilitator;

internal class X402DuplicatePropertiesFilter : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsPost(request.Method))
        {
            request.EnableBuffering();
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer);
            request.Body.Position = 0;

            if (!IsUtf8(request.ContentType) || !IsWellFormed(buffer.GetBuffer().AsMemory(0, (int)buffer.Length)))
            {
                context.Result = new BadRequestObjectResult(new { error = X402ErrorCodes.InvalidPayload });
                return;
            }
        }

        await next();
    }

    private static bool IsUtf8(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType) || !MediaTypeHeaderValue.TryParse(contentType, out var media))
            return true;

        var charset = media.Charset.Value;
        return string.IsNullOrEmpty(charset)
            || string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWellFormed(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            return !X402DuplicateJsonProperties.Contains(utf8Json);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
