 
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;
using JicSwap.ViewModels;

namespace JicSwap.Controllers.Auth;

 





public class LoginController : Controller
{
    public const int MAX_ATTEMPTS = 3;
    public const int DECAY = 900;

     
    public class LoginLock
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "student";
        public long Until { get; set; }

         
        public object? this[string key] => key switch { "email" => Email, "role" => Role, "until" => Until, _ => null };
    }

     
    [HttpGet("/login", Name = "login")]
    [RedirectIfAuthenticated("student", "admin")]
    public IActionResult Show([FromQuery] string? role)
    {
        var lk = HttpContext.SessionGet<LoginLock>("login_lock");
        if (lk != null && lk.Until <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())    
        {
            HttpContext.SessionForget("login_lock");
            lk = null;
        }
         
        var oldRole = Input.Old(TempData, "role");
        var resolved = oldRole ?? (role == "admin" ? "admin" : (lk?.Role ?? "student"));

        return View("auth.login", new { role = resolved, @lock = lk });
    }

    [HttpPost("/login", Name = "login.attempt")]
    [RedirectIfAuthenticated("student", "admin")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var role = request.Role == "admin" ? "admin" : "student";
        var email = request.Email!;
        var key = "login:" + role + ":" + email;
        var limiter = Service<RateLimiter>();

        if (limiter.TooManyAttempts(key, MAX_ATTEMPTS))
        {
            return Locked(key, role, email);
        }

        bool ok;
        int userId = 0;
        if (role == "student")
        {
            var student = Db.Students.FirstOrDefault(s => s.Email == email);
            if (student != null && student.AccountStatus == "Blocked" && Hash.Check(request.Password, student.Password))
            {
                TempData["blocked"] = email;
                return RedirectToRoute("login");    
            }
             
            var candidate = Db.Students.FirstOrDefault(s => s.Email == email && s.AccountStatus == "Active");
            ok = candidate != null && Hash.Check(request.Password, candidate.GetAuthPassword());
            userId = candidate?.StudentID ?? 0;
        }
        else
        {
            var candidate = Db.Admins.FirstOrDefault(a => a.Email == email);
            ok = candidate != null && Hash.Check(request.Password, candidate.GetAuthPassword());
            userId = candidate?.AdminID ?? 0;
        }

        if (ok)
        {
            limiter.Clear(key);
            HttpContext.SessionForget("login_lock");
            await HttpContext.LoginAsync(role, userId);
             
            return Intended(role == "admin" ? "admin.dashboard" : "browse");
        }

        limiter.Hit(key, DECAY);
        var used = limiter.Attempts(key);
        if (used >= MAX_ATTEMPTS)
        {
            return Locked(key, role, email);
        }

         
        FlashInput("Email", "role");
        FlashErrors(new Dictionary<string, string> { ["Password"] = "The email or the password is not correct." });
        TempData["attempts_used"] = used;
        return Back();
    }

    private IActionResult Locked(string key, string role, string email)
    {
        HttpContext.SessionPut("login_lock", new LoginLock
        {
            Email = email, Role = role,
            Until = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Service<RateLimiter>().AvailableIn(key),
        });
        return RedirectToRoute("login");
    }

    [HttpPost("/logout", Name = "logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("student");
        await HttpContext.SignOutAsync("admin");
        HttpContext.Session.Clear();    
        return RedirectToRoute("home");
    }
}
