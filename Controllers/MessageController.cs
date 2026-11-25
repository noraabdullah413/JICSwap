 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.ViewModels;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class MessageController : Controller
{
     
    public class Conversation
    {
        public string Key { get; set; } = string.Empty;
        public Listing Listing { get; set; } = null!;
        public Student Other { get; set; } = null!;
        public Message Last { get; set; } = null!;
        public int Unread { get; set; }

         
        public object? this[string key] => key switch { "key" => Key, "listing" => Listing, "other" => Other, "last" => Last, "unread" => Unread, _ => null };
    }

    [HttpGet("/messages", Name = "messages.index")]
    public async Task<IActionResult> Index()
    {
        var me = (await Me())!;
        return View("student.messages", new
        {
            convos = Conversations(me), active = (string?)null, thread = new List<Message>(),
            listing = (Listing?)null, other = (Student?)null, canBuy = false,
        });
    }

    [HttpGet("/messages/{listing:int}/{student:int?}", Name = "messages.show")]
    public async Task<IActionResult> Show(int listing, int? student)
    {
        var me = (await Me())!;
        var l = Db.Listings.Include(x => x.Images).Include(x => x.Category).Include(x => x.Seller).FirstOrDefault(x => x.ListingID == listing)
            ?? throw new AbortException(404);
        var iAmSeller = l.SellerID == me.StudentID;
        Student other;
        if (student == null)
        {
            if (iAmSeller)
            {
                var first = Conversations(me).FirstOrDefault(c => c.Listing.ListingID == l.ListingID);
                return first != null
                    ? RedirectToRoute("messages.show", new { listing = l.ListingID, student = first.Other.StudentID })
                    : RedirectToRoute("messages.index");
            }
            other = l.Seller!;
        }
        else
        {
            other = FindOrFail<Student>(student.Value);
        }
        AbortException.Unless(Allowed(l, me, other), 403);

         
        Db.Messages.Thread(l.ListingID, me.StudentID, other.StudentID).Where(m => m.ReceiverID == me.StudentID && !m.IsRead)
            .ExecuteUpdate(u => u.SetProperty(m => m.IsRead, true));
        var thread = Db.Messages.Thread(l.ListingID, me.StudentID, other.StudentID).OrderBy(m => m.SentDate).ThenBy(m => m.MessageID).ToList();
        var convos = Conversations(me);
        var canBuy = !iAmSeller && Service<OrderService>().BlockReason(l, me) == null;

        return View("student.messages", new
        {
            convos, active = l.ListingID + "-" + other.StudentID, thread, listing = l, other, canBuy,
        });
    }

    [HttpPost("/messages", Name = "messages.store")]
    public async Task<IActionResult> Store([FromForm] MessageRequest request)
    {
        var me = (await Me())!;
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var l = FindOrFail<Listing>(request.ListingID!.Value);
        var other = FindOrFail<Student>(request.ReceiverID!.Value);
        AbortException.Unless(Allowed(l, me, other), 403);
        if (other.AccountStatus != "Active")
        {
            TempData["error"] = "This student account is blocked and cannot receive messages.";
            return Back();
        }
        Db.Messages.Add(new Message
        {
            SenderID = me.StudentID, ReceiverID = other.StudentID, ListingID = l.ListingID,
            Content = request.Content!.Trim(), SentDate = Clock.StartOfSecond(), IsRead = false,
        });
        Db.SaveChanges();

        return RedirectToRoute("messages.show", new { listing = l.ListingID, student = other.StudentID });
    }

     
    private bool Allowed(Listing listing, Student me, Student other)
    {
        if (me.StudentID == other.StudentID)
        {
            return false;
        }
        if (other.StudentID == listing.SellerID)
        {
            return true;
        }
        if (me.StudentID != listing.SellerID)
        {
            return false;
        }

        return Db.Messages.Thread(listing.ListingID, me.StudentID, other.StudentID).Any()
            || Db.Orders.Any(o => o.BuyerID == other.StudentID && o.Items.Any(i => i.ListingID == listing.ListingID));
    }

     
    private List<Conversation> Conversations(Student me)
    {
        var msgs = Db.Messages.Include(m => m.Listing).ThenInclude(l => l!.Category).Include(m => m.Listing).ThenInclude(l => l!.Images)
            .Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderID == me.StudentID || m.ReceiverID == me.StudentID)
            .OrderByDescending(m => m.SentDate).ThenByDescending(m => m.MessageID).ToList();

        return msgs.GroupBy(m => m.ListingID + "-" + (m.SenderID == me.StudentID ? m.ReceiverID : m.SenderID))
            .Select(g =>
            {
                var last = g.First();
                return new Conversation
                {
                    Key = g.Key,
                    Listing = last.Listing!,
                    Other = last.SenderID == me.StudentID ? last.Receiver! : last.Sender!,
                    Last = last,
                    Unread = g.Count(m => m.ReceiverID == me.StudentID && !m.IsRead),
                };
            }).ToList();
    }
}
