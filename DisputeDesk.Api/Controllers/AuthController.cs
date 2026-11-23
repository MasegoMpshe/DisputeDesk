using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using DisputeDesk.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController(SignInManager<IdentityUser> signInManager, DisputeDeskDbContext database) : ControllerBase
{
    [Authorize(Policy = DisputeAccess.SignedIn)]
    [HttpGet("session")]
    public async Task<ActionResult<UserSession>> Session(CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? DisputeAccess.UserId(User);
        if (User.IsInRole(DisputeAccess.Reviewer))
        {
            return new UserSession(DisputeAccess.Reviewer, username);
        }

        var userId = DisputeAccess.UserId(User);
        var displayName = await database.Customers.AsNoTracking()
            .Where(customer => customer.IdentityUserId == userId)
            .Select(customer => customer.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        return new UserSession(DisputeAccess.Customer, string.IsNullOrWhiteSpace(displayName) ? username : displayName);
    }

    public sealed record UserSession(string Role, string DisplayName);

    [HttpPost("login")]
    [Consumes("application/json")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.UserName, request.Password, isPersistent: false, lockoutOnFailure: true);

        return result.Succeeded ? NoContent() : Unauthorized();
    }

    [Authorize(Policy = DisputeAccess.SignedIn)]
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
