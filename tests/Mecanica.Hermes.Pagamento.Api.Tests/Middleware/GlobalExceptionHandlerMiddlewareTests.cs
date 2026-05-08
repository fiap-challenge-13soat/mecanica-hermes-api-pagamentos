using System.Net;
using System.Text.Json;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Middleware;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Adapters;
using Mecanica.Hermes.Shared.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Middleware;

public class GlobalExceptionHandlerMiddlewareTests
{
    private sealed class StubHostEnvironment(string envName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = envName;
        public string ApplicationName { get; set; } = "Mecanica.Hermes.Pagamento.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (DefaultHttpContext context, MemoryStream body) BuildContext()
    {
        var ctx = new DefaultHttpContext();
        var stream = new MemoryStream();
        ctx.Response.Body = stream;
        return (ctx, stream);
    }

    private static async Task<JsonElement> ReadBodyAsJson(MemoryStream body)
    {
        body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(body);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_DoesNotWriteResponse()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        var (ctx, body) = BuildContext();
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => Task.CompletedTask,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(ctx);

        body.Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_OnUnhandledException_WritesProblemDetailsWith500()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        var (ctx, body) = BuildContext();
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("falha generica"),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        ctx.Response.ContentType.Should().Be("application/problem+json");

        var json = await ReadBodyAsJson(body);
        json.GetProperty("status").GetInt32().Should().Be(500);
        json.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("detail").GetString().Should().Contain("falha generica");
        json.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task InvokeAsync_OnArgumentException_WritesProblemDetailsWith400()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        var (ctx, body) = BuildContext();
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new ArgumentException("argumento ruim"),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var json = await ReadBodyAsJson(body);
        json.GetProperty("status").GetInt32().Should().Be(400);
        json.GetProperty("title").GetString().Should().Be("Argumento Inválido");
    }

    [Fact]
    public async Task InvokeAsync_OnUnauthorizedAccessException_WritesProblemDetailsWith401()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        var (ctx, body) = BuildContext();
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new UnauthorizedAccessException("acesso negado"),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        var json = await ReadBodyAsJson(body);
        json.GetProperty("status").GetInt32().Should().Be(401);
        json.GetProperty("title").GetString().Should().Be("Não Autorizado");
    }

    [Fact]
    public async Task InvokeAsync_OnBrokerUnavailable_WritesProblemDetailsWith503()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        var (ctx, body) = BuildContext();
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new BrokerUnavailableException("broker fora"),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
        var json = await ReadBodyAsJson(body);
        json.GetProperty("status").GetInt32().Should().Be(503);
        json.GetProperty("title").GetString().Should().Be("Serviço Indisponível");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_HidesStackTrace()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Production);
        var (ctx, body) = BuildContext();
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("segredo de stack"),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            env);

        await middleware.InvokeAsync(ctx);

        var json = await ReadBodyAsJson(body);
        json.GetProperty("title").GetString().Should().Be("Erro Interno");
        json.GetProperty("detail").GetString().Should().NotContain("segredo de stack");
    }
}
