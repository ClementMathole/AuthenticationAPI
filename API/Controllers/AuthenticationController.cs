using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static Application.DTOs.AuthDtos;
using LoginRequest = Application.DTOs.AuthDtos.LoginRequest;
using RefreshRequest = Application.DTOs.AuthDtos.RefreshRequest;
using RegisterRequest = Application.DTOs.AuthDtos.RegisterRequest;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthService _service;
        private readonly IUserRepository _user;

        public AuthenticationController(IAuthService service, IUserRepository user)
        {
            _service = service;
            _user = user;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            await _service.RegisterAsync(request);
            return Accepted(new { message = "Registered Successfully, Confirm Email" });
        }

        [HttpGet("confirm")]
        public async Task<IActionResult> Confirm([FromQuery] Guid userId, [FromQuery] string token)
        {
            var rt = await _user.GetRefreshTokenAsync(token);
            if (rt is null || rt.UserId != userId || rt.Revoked || rt.Expires <= DateTime.UtcNow)
                return BadRequest(new { message = "Invalid or expired token" });

            var user = await _user.GetByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.EmailConfirmed = true;
            await _user.UpdateAsync(user);
            await _user.RevokeRefreshTokenAsync(rt);

            return Ok(new { message = "Email confirmed" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            try
            {
                var auth = await _service.LoginAsync(req, ip);
                return Ok(auth);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(423, new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest req)
        {
            try
            {
                var auth = await _service.RefreshAsync(req);
                return Ok(auth);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid or expired refresh token" });
            }
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke(RevokeRequest request)
        {
            var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(subject, out var authenticatedUserId) || authenticatedUserId == Guid.Empty)
                return Unauthorized();

            await _service.RevokeAsync(request, authenticatedUserId);
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var email = User?.Identity?.Name;
            if (email == null)
                return Unauthorized();

            var u = await _user.GetByEmailAsync(email);
            if (u == null)
                return NotFound();

            return Ok(new { u.Id, u.Email, u.Username, u.Role });
        }
    }
}
