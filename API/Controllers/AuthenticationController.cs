using Application.Exceptions;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Application.DTOs.AuthDtos;

namespace API.Controllers;

[ApiController]
[Route("api/authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthenticationController(IAuthService service, IUserRepository users) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await service.RegisterAsync(request, ct);
        return Accepted(new
        {
            message = "If registration is available, a confirmation email has been sent."
        });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken ct)
    {
        await service.ResendConfirmationAsync(request, ct);
        return Accepted(new
        {
            message = "If confirmation is needed, a new email has been sent."
        });
    }

    [HttpGet("confirm")]
    public async Task<IActionResult> Confirm([FromQuery] Guid userId, [FromQuery] string token, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!await service.ConfirmEmailAsync(userId, token, ct))
            return BadRequest(new
            {
                message = "Invalid or expired confirmation token"
            });

        return Ok(new { message = "Email confirmed" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await service.LoginAsync(request, ct));
        }
        catch (AccountLockedException)
        {
            return StatusCode(423, new
            {
                message = "Account temporarily locked. Try again later."
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new
            {
                message = "Invalid credentials or account not confirmed"
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await service.RefreshAsync(request, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new
            {
                message = "Invalid or expired refresh token"
            });
        }
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RevokeRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await service.RevokeAsync(request, userId, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
            return Unauthorized();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Username,
            role = user.Role.ToString()
        });
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
