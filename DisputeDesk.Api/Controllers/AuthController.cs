using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController(SignInManager<IdentityUser> signInManager) : ControllerBase
{
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
    public async Task<IActionResult> Logout()
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
