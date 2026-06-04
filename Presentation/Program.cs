using AIService;
using AIService.Options;
using BusinessLogic;
using BusinessLogic.Options;
using DataAcessLayer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Presentation.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddDataAccess(connectionString);
GeminiOptions geminiOptions = builder.Configuration
    .GetSection("Gemini")
    .Get<GeminiOptions>() ?? new GeminiOptions();
RagDebugOptions ragDebugOptions = builder.Configuration
    .GetSection("RagDebug")
    .Get<RagDebugOptions>() ?? new RagDebugOptions();
builder.Services.AddAiService(geminiOptions);
// Truyền cấu hình RAG debug vào BusinessLogic để mặc định không log nội dung nhạy cảm.
builder.Services.AddBusinessLogic(ragDebugOptions);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "DocXM.Session";
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "DocXM.Auth";
        options.SlidingExpiration = true;
    });
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    // File upload chỉ được đọc qua controller sau khi kiểm tra quyền.
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Subject}/{action=Index}/{id?}");

app.Run();
