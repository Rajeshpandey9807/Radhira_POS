using PosApp.Web.Data;
using PosApp.Web.Features.BusinessTypes;
using PosApp.Web.Features.BusinessProfiles;
using PosApp.Web.Features.Dashboard;
using PosApp.Web.Features.IndustryTypes;
using PosApp.Web.Features.Roles;
using PosApp.Web.Features.RegistrationTypes;
using PosApp.Web.Features.Users;
using PosApp.Web.Features.States;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Cookie.Name = "radhira-pos.auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<RoleMasterService>();
builder.Services.AddScoped<BusinessTypeService>();
builder.Services.AddScoped<IndustryTypeService>();
builder.Services.AddScoped<RegistrationTypeService>();
builder.Services.AddScoped<StateService>();
builder.Services.AddScoped<BusinessProfileService>();
builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
