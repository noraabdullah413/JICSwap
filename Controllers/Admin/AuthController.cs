 
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace JicSwap.Controllers.Admin;

 
public class AuthController : Controller
{
     



    [HttpGet("/admin/login", Name = "admin.login")]
    public IActionResult Login()
    {
        return RedirectToRoute("login", new { role = "admin" });
    }

    [HttpPost("/admin/logout", Name = "admin.logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("admin");
        HttpContext.Session.Clear();    
        return RedirectToRoute("login", new { role = "admin" });
    }
}
