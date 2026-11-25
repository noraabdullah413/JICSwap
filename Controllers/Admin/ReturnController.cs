 
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Services;
using JicSwap.ViewModels.Admin;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class ReturnController : Controller
{
    public static readonly string[] TABS = { "Pending", "Approved", "Rejected" };

     
    private IQueryable<ReturnRequest> WithOrder(IQueryable<ReturnRequest> q) =>
        q.Include(r => r.Order).ThenInclude(o => o!.Buyer)
         .Include(r => r.Order).ThenInclude(o => o!.Payment)
         .Include(r => r.Order).ThenInclude(o => o!.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
         .Include(r => r.Order).ThenInclude(o => o!.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
         .Include(r => r.Order).ThenInclude(o => o!.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
         .Include(r => r.Admin);

    [HttpGet("/admin/returns", Name = "admin.returns.index")]
    public IActionResult Index([FromQuery] string? status)
    {
        status = TABS.Contains(status) ? status! : "Pending";
        var counts = Db.ReturnRequests.GroupBy(r => r.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        var returns = PaginatedList<ReturnRequest>.Create(WithOrder(Db.ReturnRequests).Where(r => r.Status == status).OrderByDescending(r => r.RequestDate), Request, 15);

        return View("admin.returns", new { returns, counts, status });
    }

    [HttpGet("/admin/returns/{return:int}", Name = "admin.returns.show")]
    public IActionResult Show([FromRoute(Name = "return")] int returnId)
    {
        var r = WithOrder(Db.ReturnRequests).FirstOrDefault(x => x.ReturnID == returnId) ?? throw new AbortException(404);
        return View("admin.return-show", new { r });
    }

    [HttpPost("/admin/returns/{return:int}/approve", Name = "admin.returns.approve")]
    public async Task<IActionResult> Approve([FromRoute(Name = "return")] int returnId, [FromForm] ReturnDecisionRequest request)
    {
        var r = FindOrFail<ReturnRequest>(returnId);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        AbortException.Unless(r.Status == "Pending", 409);
        Service<AdminActions>().ApproveReturn(r, (await AdminUser())!, double.Parse(request.RefundAmount!, CultureInfo.InvariantCulture));

        TempData["flash"] = "The return is approved and the payment is marked as refunded.";
        return RedirectToRoute("admin.returns.show", new { @return = r.ReturnID });
    }

    [HttpPost("/admin/returns/{return:int}/reject", Name = "admin.returns.reject")]
    public async Task<IActionResult> Reject([FromRoute(Name = "return")] int returnId, [FromForm] ReturnDecisionRequest request)
    {
        var r = FindOrFail<ReturnRequest>(returnId);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        AbortException.Unless(r.Status == "Pending", 409);
        Service<AdminActions>().RejectReturn(r, (await AdminUser())!, request.Reason!.Trim());

        TempData["flash"] = "The return is rejected. Both students were informed by email.";
        return RedirectToRoute("admin.returns.show", new { @return = r.ReturnID });
    }
}
