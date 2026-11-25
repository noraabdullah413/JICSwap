 
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JicSwap.Data;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;
using JicSwap.Support.Providers;
using JicSwap.Support.Routes;
using JicSwap.Support.Services;
using JicSwap.Support.Services.Payments;
using ConsoleKernel = JicSwap.Support.Console.Kernel;
using HttpKernel = JicSwap.Support.Http.Kernel;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

 
Php.AppName = configuration["App:Name"] ?? "JIC Swap";
try { Clock.Zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["App:Timezone"] ?? "Asia/Riyadh"); } catch (TimeZoneNotFoundException) {   }
SwapConfig.Bind(configuration.GetSection("Swap"));
Hash.Rounds = configuration.GetValue("Hashing:Bcrypt:Rounds", 12);

 
 
services.AddDbContext<AppDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

 
AppServiceProvider.Register(services, configuration);
AppServiceProvider.Boot(services);
AuthServiceProvider.Boot(services);         
RouteServiceProvider.Boot(services);        
EventServiceProvider.Boot(services);
BroadcastServiceProvider.Boot(services);
EncryptCookies.Register(services);          

 
services.AddHttpContextAccessor();
services.AddMemoryCache();                  
services.AddDistributedMemoryCache();
services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(configuration.GetValue("Session:Lifetime", 15));    
    o.Cookie.Name = configuration["Session:Cookie"] ?? "jic_swap_session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    o.Cookie.IsEssential = true;
});

 
services.AddSingleton<RateLimiter>();
services.AddSingleton<ValidateSignature>();
services.AddScoped<UploadsDisk>();
services.AddScoped<MailSender>();
services.AddScoped<OrderService>();
services.AddScoped<FeatureService>();
services.AddScoped<AdminActions>();
services.AddScoped<NotificationService>();
services.AddScoped<ListingWizard>();
services.AddScoped<BladeHelpers>();
services.AddScoped<EnsureStudentActive>();            
services.AddSingleton<ConsoleKernel>();               
 
if (configuration.GetValue("Schedule:Enabled", false))
{
    services.AddHostedService(sp => sp.GetRequiredService<ConsoleKernel>());
}

 
services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(HttpKernel.ConfigureForwardedHeaders);
services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));    

services.AddControllersWithViews(o =>
{
    VerifyCsrfToken.Register(o);                      
    o.Filters.Add<AbortExceptionFilter>();            
})
.AddSessionStateTempDataProvider();                   

var app = builder.Build();

 
if (args.Length > 0 && ConsoleRoutes.Purposes.ContainsKey(args[0]))
{
    await app.Services.GetRequiredService<ConsoleKernel>().Run(args[0]);
    return;
}

 
Handler.Register(app);
app.UseStaticFiles();                                 
HttpKernel.UseGlobalMiddleware(app);
app.UseMiddleware<MethodOverride>();                  
app.UseRouting();
HttpKernel.UseWebGroup(app);                          

 
 
 
 
 
app.MapControllers();

 
app.Map("/error/{code:int}", (int code) => Results.Text(code == 404 ? "Not Found" : code == 403 ? "Forbidden" : "Error " + code, "text/plain", statusCode: code));

app.Run();
