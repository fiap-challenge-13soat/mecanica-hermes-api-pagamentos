using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-ID";

    [Fact]
    public async Task InvokeAsync_WhenHeaderPresent_PropagatesToResponse()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = "abc-123";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers[HeaderName].ToString().Should().Be("abc-123");
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderAbsent_GeneratesGuidAndAddsToResponse()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        var generated = context.Response.Headers[HeaderName].ToString();
        generated.Should().NotBeNullOrEmpty();
        Guid.TryParse(generated, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderEmpty_GeneratesNewId()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);
        var context = new DefaultHttpContext();
        // Setting an empty header value: FirstOrDefault on StringValues will return null,
        // triggering the generated GUID branch.
        context.Request.Headers[HeaderName] = Microsoft.Extensions.Primitives.StringValues.Empty;

        await middleware.InvokeAsync(context);

        var generated = context.Response.Headers[HeaderName].ToString();
        generated.Should().NotBeNullOrEmpty();
    }
}
