 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JicSwap.Support;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class NotificationController : Controller
{
    [HttpGet("/notifications", Name = "notifications.index")]
    public async Task<IActionResult> Index()
    {
        var notifications = Service<NotificationService>();
        var items = notifications.Items((await Me())!);
        var seen = notifications.SeenAt();
        HttpContext.Session.SetString("notifications_seen_at", Clock.Now().ToString("yyyy-MM-dd HH:mm:ss"));

        return View("student.notifications", new { items, seen });
    }
}
