using System.Security.Claims;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Middleware;
using Mecanica.Hermes.Pagamento.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Middleware;

public class DevelopmentAuthenticationMiddlewareTests
{
    private sealed class StubHostEnvironment(string envName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = envName;
        public string ApplicationName { get; set; } = "Mecanica.Hermes.Pagamento.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_SetsClaimsPrincipalWithExpectedClaims()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DevelopmentAuthenticationMiddleware(next, env);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.User.Should().NotBeNull();
        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("test-user-id");
        context.User.FindFirstValue(ClaimTypes.Name).Should().Be("Test User");
        context.User.FindFirstValue(ClaimTypes.Email).Should().Be("test@example.com");
        var scope = context.User.FindFirstValue("scope");
        scope.Should().NotBeNullOrEmpty();
        scope.Should().Contain(" "); // contains both admin and cliente scope, separated by space
    }

    [Fact]
    public async Task InvokeAsync_InTesting_SetsClaimsPrincipal()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Testing);
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new DevelopmentAuthenticationMiddleware(next, env);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.User.Identity!.IsAuthenticated.Should().BeTrue();
        context.User.FindFirstValue("scope").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_InProduction_Throws()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Production);
        RequestDelegate next = _ => Task.CompletedTask;

        Action act = () => _ = new DevelopmentAuthenticationMiddleware(next, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Production*");
    }

    [Fact]
    public async Task InvokeAsync_WhenUserAlreadyAuthenticated_DoesNotOverride()
    {
        var env = new StubHostEnvironment(EnvironmentNames.Development);
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new DevelopmentAuthenticationMiddleware(next, env);

        var existingIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Already Authenticated User")
        }, "ExistingAuth");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(existingIdentity)
        };

        await middleware.InvokeAsync(context);

        context.User.FindFirstValue(ClaimTypes.Name).Should().Be("Already Authenticated User");
        context.User.FindFirstValue(ClaimTypes.NameIdentifier).Should().BeNull();
    }
}
