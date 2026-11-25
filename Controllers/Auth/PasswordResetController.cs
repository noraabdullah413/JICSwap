 
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.ViewModels;

namespace JicSwap.Controllers.Auth;

 



[RedirectIfAuthenticated("student")]
public class PasswordResetController : Controller
{
    private static string Sha1(string value) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [HttpGet("/forgot-password", Name = "password.request")]
    public IActionResult Request_()
    {
        return View("auth.forgot");
    }

    [HttpPost("/forgot-password", Name = "password.email")]
    public IActionResult Email([FromForm] ForgotPasswordRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var email = request.Email!;
        var student = Db.Students.FirstOrDefault(s => s.Email == email && s.AccountStatus == "Active");
        if (student != null)
        {
             
            var path = Url.RouteUrl("password.reset", new { student = student.StudentID, hash = Sha1(student.Password) })!;
            var signed = Service<ValidateSignature>().Sign(path, DateTime.UtcNow.AddMinutes(60));
            var url = $"{Request.Scheme}://{Request.Host}{signed}";
            Service<MailSender>().Raw(email, "Reset your JIC Swap password",
                $"Open this link to choose a new JIC Swap password (valid for 60 minutes):\n\n{url}\n\n"
                + "If you did not ask for it, you can ignore this email.");
        }

        TempData["sent_to"] = email;    
        return Back();
    }

    [HttpGet("/reset-password/{student:int}", Name = "password.reset")]
    public IActionResult Edit(int student)
    {
        var s = FindOrFail<Student>(student);
        return View("auth.reset", new { student = s, valid = Valid(s) });
    }

    [HttpPost("/reset-password/{student:int}", Name = "password.update")]
    public IActionResult Update(int student, [FromForm] ResetPasswordRequest request)
    {
        var s = FindOrFail<Student>(student);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        if (!Valid(s))
        {
            return Back();
        }
        s.Password = Hash.Make(request.Password!);
        Db.SaveChanges();

        TempData["status"] = "Your password has been changed. Sign in with the new password.";
        return RedirectToRoute("login");
    }

    private bool Valid(Student student)
    {
        var hash = Request.Query["hash"].ToString();
        return Service<ValidateSignature>().HasValidSignature(Request)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sha1(student.Password)), Encoding.UTF8.GetBytes(hash))
            && student.AccountStatus == "Active";
    }
}
