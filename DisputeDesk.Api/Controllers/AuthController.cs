using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController(SignInManager<IdentityUser> signInManager) : ControllerBase
{
    [Authorize]
    [HttpGet("session")]
    public IActionResult Session() =>
        Ok(new { role = User.IsInRole(DisputeAccess.Reviewer) ? DisputeAccess.Reviewer : DisputeAccess.Customer });

    [HttpPost("login")]
    [Consumes("application/json")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.UserName, request.Password, isPersistent: false, lockoutOnFailure: true);

        return result.Succeeded ? NoContent() : Unauthorized();
    }

    [Authorize]
    [HttpPost("logout")]
    [Consumes("application/json")]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement _ = default)
    {
        if (!Request.HasJsonContentType())
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        await signInManager.SignOutAsync();
        return NoContent();
    }

    public sealed record LoginRequest(
        [Required, StringLength(256)] string UserName,
        [Required, StringLength(256)] string Password);
}
