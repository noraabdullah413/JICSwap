 
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using JicSwap.Data;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using MvcController = Microsoft.AspNetCore.Mvc.Controller;
using AdminModel = JicSwap.Models.Admin;    

namespace JicSwap.Controllers;

 




public class Controller : MvcController
{
     
    protected AppDbContext Db => HttpContext.RequestServices.GetRequiredService<AppDbContext>();

    protected T Service<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

     
    protected Task<Student?> Me() => HttpContext.CurrentStudentAsync(Db);

     
    protected Task<AdminModel?> AdminUser() => HttpContext.CurrentAdminAsync(Db);

     
    protected T FindOrFail<T>(int id) where T : class => Db.Set<T>().Find(id) ?? throw new AbortException(404);

     



    protected new ViewResult View(string name, object? data = null)
    {
        if (data != null)
        {
            if (data is IDictionary<string, object?> dict)
            {
                foreach (var (k, v) in dict) ViewData[k] = v;
            }
            else
            {
                foreach (var p in data.GetType().GetProperties()) ViewData[p.Name] = p.GetValue(data);
            }
        }
        return base.View("~/Views/" + name.Replace('.', '/') + ".cshtml");
    }

     
    protected void Flash(string key, object? value) => TempData[key] = value;

     
    protected IActionResult Back() => Redirect(string.IsNullOrEmpty(Request.Headers.Referer) ? "/" : Request.Headers.Referer.ToString());

     
    protected IActionResult Intended(string routeName)
    {
        var url = HttpContext.Session.GetString("url.intended");
        if (!string.IsNullOrEmpty(url))
        {
            HttpContext.Session.Remove("url.intended");
            return Redirect(url);
        }
        return RedirectToRoute(routeName);
    }

    protected IActionResult Abort(int code, string? message = null) => throw new AbortException(code, message);

     

    private static readonly Dictionary<Type, Dictionary<string, string>> FieldNames = new();

     
    private static Dictionary<string, string> NamesOf(Type t)
    {
        lock (FieldNames)
        {
            if (FieldNames.TryGetValue(t, out var cached)) return cached;
            var map = new Dictionary<string, string>();
            foreach (var p in t.GetProperties())
            {
                var from = p.GetCustomAttributes().OfType<IModelNameProvider>().FirstOrDefault(a => !string.IsNullOrEmpty(a.Name));
                map[p.Name] = from?.Name ?? p.Name;
            }
            return FieldNames[t] = map;
        }
    }

     




    protected IActionResult? ValidateRequest(FormRequestBase request)
    {
        AbortException.Unless(request.Authorize(HttpContext), 403);
        request.PrepareForValidation(HttpContext);

        ModelState.Clear();
        var results = new List<ValidationResult>();
        var names = NamesOf(request.GetType());
        Validator.TryValidateObject(request, new ValidationContext(request, HttpContext.RequestServices, null), results, validateAllProperties: true);
        foreach (var r in results)
        {
            foreach (var member in r.MemberNames.DefaultIfEmpty(string.Empty))
            {
                ModelState.AddModelError(names.TryGetValue(member, out var field) ? field : member, r.ErrorMessage ?? string.Empty);
            }
        }
        request.DynamicRules(HttpContext, ModelState);
        request.WithValidator(HttpContext, ModelState, results.Count == 0);

        if (ModelState.IsValid) return null;

        request.FailedValidation(HttpContext, ModelState);
        return BackWithErrors(request.ErrorBag);
    }

     
    protected IActionResult BackWithErrors(string bag = "default")
    {
        var errors = ModelState.Where(e => e.Value!.Errors.Count > 0)
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToList());
        TempData["__errors:" + bag] = JsonSerializer.Serialize(errors);
        var old = new Dictionary<string, string[]>();
        if (Request.HasFormContentType)
        {
            foreach (var kv in Request.Form.Where(kv => !JicSwap.Support.Exceptions.Handler.DontFlash.Contains(kv.Key, StringComparer.OrdinalIgnoreCase)))
            {
                old[kv.Key] = kv.Value.Select(v => v ?? string.Empty).ToArray();
            }
        }
        TempData["__old"] = JsonSerializer.Serialize(old);
        return Back();
    }

     
    protected void FlashInput(params string[] keys)
    {
        var old = new Dictionary<string, string[]>();
        if (Request.HasFormContentType)
        {
            foreach (var k in keys) if (Request.Form.ContainsKey(k)) old[k] = Request.Form[k].Select(v => v ?? string.Empty).ToArray();
        }
        TempData["__old"] = JsonSerializer.Serialize(old);
    }

     
    protected void FlashErrors(IDictionary<string, string> errors, string bag = "default")
    {
        TempData["__errors:" + bag] = JsonSerializer.Serialize(errors.ToDictionary(e => e.Key, e => new List<string> { e.Value }));
    }

     
    protected IActionResult BackWithErrors(IDictionary<string, string> errors, bool withInput = true, string bag = "default")
    {
        TempData["__errors:" + bag] = JsonSerializer.Serialize(errors.ToDictionary(e => e.Key, e => new List<string> { e.Value }));
        if (withInput && Request.HasFormContentType)
        {
            TempData["__old"] = JsonSerializer.Serialize(Request.Form
                .Where(kv => !JicSwap.Support.Exceptions.Handler.DontFlash.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Select(v => v ?? string.Empty).ToArray()));
        }
        return Back();
    }
}
