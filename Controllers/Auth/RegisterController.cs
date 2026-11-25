 
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.ViewModels;

namespace JicSwap.Controllers.Auth;

 



public class RegisterController : Controller
{
     
    public class Registration
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? PhoneTyped { get; set; }
        public string Major { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Verified { get; set; }

         
        public object? this[string key] => key switch
        {
            "FirstName" => FirstName, "LastName" => LastName, "Email" => Email, "PhoneNumber" => PhoneNumber, "PhoneTyped" => PhoneTyped,
            "Major" => Major, "Password" => Password, "verified" => Verified, _ => null,
        };
    }

     
    private class CodeEntry
    {
        public string Hash { get; set; } = string.Empty;
        public long Expires { get; set; }
        public int Tries { get; set; }
        public long SentAt { get; set; }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();    

     
    [HttpGet("/register", Name = "register")]
    [RedirectIfAuthenticated("student")]
    public IActionResult Create()
    {
        return View("auth.register", new { reg = HttpContext.SessionGet<Registration>("registration") });
    }

    [HttpPost("/register", Name = "register.store")]
    [RedirectIfAuthenticated("student")]
    public IActionResult Store([FromForm] RegisterRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        HttpContext.SessionPut("registration", new Registration
        {
            FirstName = request.FirstName!, LastName = request.LastName!, Email = request.Email!,
            PhoneNumber = request.PhoneNormalised, PhoneTyped = request.PhoneNumber, Major = request.Major!,
            Password = Hash.Make(request.Password!), Verified = false,
        });
        SendCode(request.Email!);

        return RedirectToRoute("register.verify");
    }

    [HttpGet("/register/verify", Name = "register.verify")]
    [RedirectIfAuthenticated("student")]
    public IActionResult VerifyForm()
    {
        var reg = HttpContext.SessionGet<Registration>("registration");
        if (reg == null) return RedirectToRoute("register");
        if (reg.Verified) return RedirectToRoute("register.username");

        var entry = Service<IMemoryCache>().Get<CodeEntry>("reg:" + reg.Email);
        var left = entry != null ? Math.Max(0, entry.Expires - Now()) : 0;

        return View("auth.verify", new { email = reg.Email, secondsLeft = left });
    }

    [HttpPost("/register/verify", Name = "register.verify.check")]
    [RedirectIfAuthenticated("student")]
    public IActionResult Verify([FromForm] VerifyCodeRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var reg = HttpContext.SessionGet<Registration>("registration");
        if (reg == null) return RedirectToRoute("register");

        var cache = Service<IMemoryCache>();
        var key = "reg:" + reg.Email;
        var entry = cache.Get<CodeEntry>(key);
        if (entry == null || entry.Expires < Now())
        {
            return BackWithErrors(new Dictionary<string, string> { ["code"] = "This code has expired. Resend the code to get a new one." }, withInput: false);
        }
        if (entry.Tries >= 5)
        {
            return BackWithErrors(new Dictionary<string, string> { ["code"] = "Too many wrong codes. Resend the code to get a new one." }, withInput: false);
        }
        if (!Hash.Check(request.Code, entry.Hash))
        {
            entry.Tries++;
            cache.Set(key, entry, TimeSpan.FromSeconds(Math.Max(1, entry.Expires - Now())));
            return BackWithErrors(new Dictionary<string, string>
            {
                ["code"] = entry.Tries >= 5
                    ? "Too many wrong codes. Resend the code to get a new one."
                    : "This code is not correct. Check the code and try again.",
            }, withInput: false);
        }

        cache.Remove(key);    
        reg.Verified = true;
        HttpContext.SessionPut("registration", reg);

        return RedirectToRoute("register.username");
    }

    [HttpPost("/register/resend", Name = "register.resend")]
    [RedirectIfAuthenticated("student")]
    public IActionResult Resend()
    {
        var reg = HttpContext.SessionGet<Registration>("registration");
        if (reg == null) return RedirectToRoute("register");

        var entry = Service<IMemoryCache>().Get<CodeEntry>("reg:" + reg.Email);
        if (entry != null && entry.SentAt > Now() - 60)
        {
            return BackWithErrors(new Dictionary<string, string> { ["code"] = "A new code can be sent once a minute. Wait a moment and try again." }, withInput: false);
        }
        SendCode(reg.Email);
        TempData["resent"] = true;    
        return Back();
    }

    [HttpGet("/register/username", Name = "register.username")]
    [RedirectIfAuthenticated("student")]
    public IActionResult UsernameForm()
    {
        var reg = HttpContext.SessionGet<Registration>("registration");
        if (reg == null || !reg.Verified)
        {
            return RedirectToRoute(reg != null ? "register.verify" : "register");
        }
         
        var local = reg.Email.Substring(0, reg.Email.IndexOf('@')).ToLowerInvariant();
        var suggest = new string(local.Where(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_').ToArray());
        if (suggest.Length > 20) suggest = suggest.Substring(0, 20);

        return View("auth.username", new { reg, suggest });
    }

     
    [HttpGet("/register/username/check", Name = "register.username.check")]
    [RedirectIfAuthenticated("student")]
    public IActionResult Check([FromQuery] string? u)
    {
        u ??= string.Empty;
        var ok = System.Text.RegularExpressions.Regex.IsMatch(u, "^[a-z0-9._]{3,20}$")
            && !SwapConfig.Options.ReservedUsernames.Contains(u)
            && !Db.Students.Any(s => s.Username == u);
        return Json(new { available = ok });
    }

    [HttpPost("/register/username", Name = "register.activate")]
    [RedirectIfAuthenticated("student")]
    public async Task<IActionResult> Activate([FromForm] UsernameRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var reg = HttpContext.SessionGet<Registration>("registration");
        if (reg == null || !reg.Verified) return RedirectToRoute("register");

        var student = new Student
        {
            FirstName = reg.FirstName, LastName = reg.LastName, Username = request.Username!, Email = reg.Email,
            Password = reg.Password, PhoneNumber = reg.PhoneNumber!, Major = reg.Major, AccountStatus = "Active", JoinDate = Clock.Today(),
        };
        Db.Students.Add(student);
        Db.SaveChanges();

        HttpContext.SessionForget("registration");
        await HttpContext.LoginAsync("student", student.StudentID);    

        return RedirectToRoute("register.done");
    }

     
    [HttpGet("/register/done", Name = "register.done")]
    [Authorize(AuthenticationSchemes = "student")]
    public async Task<IActionResult> Done()
    {
        return View("auth.done", new { student = await Me() });
    }

    private void SendCode(string email)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();    
        Service<IMemoryCache>().Set("reg:" + email, new CodeEntry
        {
            Hash = Hash.Make(code), Expires = Now() + 600, Tries = 0, SentAt = Now(),
        }, TimeSpan.FromSeconds(600));
        Service<MailSender>().Raw(email, "JIC Swap verification code: " + code,
            $"Your JIC Swap verification code is {code}.\n\nThe code is valid for 10 minutes. "
            + "If you did not ask for it, you can ignore this email.\n\nJIC Swap - Jubail Industrial College");
    }
}
