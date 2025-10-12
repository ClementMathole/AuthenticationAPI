using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;
using System.Net.Http.Json;
using static Application.DTOs.AuthDtos;
using LoginRequest = Application.DTOs.AuthDtos.LoginRequest;
using RefreshRequest = Application.DTOs.AuthDtos.RefreshRequest;
using RegisterRequest = Application.DTOs.AuthDtos.RegisterRequest;

namespace API.IntegrationTests;

public class AuthenticationFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthenticationFlowTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_Login_GetProfile_Succeed()
    {
        // Registering a new user
        var registerReq = new RegisterRequest
            (
                "johndoe",
                "john@test.com",
                "Johndoe000!"
            );

        var registerResp = await _client.PostAsJsonAsync("/api/auth/register", registerReq);
        registerResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // User login
        var loginReq = new LoginRequest
            (
                "john@test.com",
                "Johndoe000!"
            );
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();

        //me endpoint
        _client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var me = await _client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meJson = await me.Content.ReadAsStringAsync();
        meJson.Should().Contain("john@test.com");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldFail_AndLockAfterFiveAttempts()
    {
        var email = "locktest@test.com";
        var register = new RegisterRequest
            (
                "lockuser",
                email,
                "Locktest000!"
            );
        await _client.PostAsJsonAsync("/api/auth/register", register);

        for (int i = 1; i <= 5; i++)
        {
            var badLogin = new LoginRequest(email, "WrongPasstest");
            var resp = await _client.PostAsJsonAsync("/api/auth/login", badLogin);
            if (i < 5)
                resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            else
                resp.StatusCode.Should().Be((HttpStatusCode)423);
        }
    }

    [Fact]
    public async Task Refresh_Then_Revoke_ShouldWork()
    {
        var register = new RegisterRequest
            (
                "refreshuser",
                "refresh@test.com",
                "Refresh000!"
            );
        await _client.PostAsJsonAsync("/api/auth/register", register);

        var login = new LoginRequest
            (
                "refresh@test.com",
                "Refresh000!"
            );

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", login);
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();

        // Refresh token
        var refreshResp = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth!.RefreshToken));
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await refreshResp.Content.ReadFromJsonAsync<AuthResponse>();
        newTokens!.AccessToken.Should().NotBe(auth.AccessToken);

        // Revoke tokrn
        var revoke = await _client.PostAsJsonAsync("/api/auth/revoke-refresh", new RefreshRequest(newTokens.RefreshToken));
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);

        // Refreshing Token again
        var second = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(newTokens.RefreshToken));
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
