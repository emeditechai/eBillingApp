using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Middleware;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RestaurantManagementSystem.Utilities;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using System.Collections.Generic;
using Microsoft.AspNetCore.StaticFiles;

namespace RestaurantManagementSystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.AddService<EnforceMenuPermissionFilter>();
            });
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpContextAccessor();

            // Session storage (used for POS counter selection and other per-user state)
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(12);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            // Add authentication services
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromHours(12);
                    options.SlidingExpiration = true;
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.Cookie.HttpOnly = true;
                    // Use SameAsRequest so cookies work when the app is behind a reverse proxy
                    // that terminates TLS or when running on HTTP for local/testing.
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    // Use Lax to allow common external navigation flows while still protecting from CSRF
                    options.Cookie.SameSite = SameSiteMode.Lax;
                });

            // Add authorization services
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Administrator"));
                options.AddPolicy("RequireManagerRole", policy => policy.RequireRole("Administrator", "Manager"));
                options.AddPolicy("RequireStaffRole", policy => policy.RequireRole("Administrator", "Manager", "Staff"));
            });

            // Register custom services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<UserRoleService>();
            builder.Services.AddScoped<RolePermissionService>();
            builder.Services.AddScoped<EnforceMenuPermissionFilter>();
            builder.Services.AddScoped<AdminSetupService>();
            builder.Services.AddScoped<PasswordResetTool>();
            builder.Services.AddScoped<UrlEncryptionService>();
            builder.Services.AddScoped<IEmailSender, DatabaseEmailSender>();
            builder.Services.AddScoped<IDayClosingService, DayClosingService>();
            // Hosted service for non-blocking admin initialization
            builder.Services.AddHostedService<AdminInitializationHostedService>();

            // Configure SQL Server database connection using connection string from appsettings.json
            builder.Services.AddDbContext<RestaurantDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            var app = builder.Build();

            // Ensure the application uses Indian culture formatting by default
            // This makes currency formatting (ToString("C")) render ₹ and Indian number formats
            var defaultCulture = new CultureInfo("en-IN");
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                SupportedCultures = new List<CultureInfo> { defaultCulture },
                SupportedUICultures = new List<CultureInfo> { defaultCulture }
            };
            app.UseRequestLocalization(localizationOptions);

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            else
            {
                // In development, enable detailed error pages
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            var staticFileContentTypes = new FileExtensionContentTypeProvider();
            if (!staticFileContentTypes.Mappings.ContainsKey(".avif"))
            {
                staticFileContentTypes.Mappings[".avif"] = "image/avif";
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = staticFileContentTypes
            });
            app.UseRouting();

            app.UseSession();

            app.UseMiddleware<DatabaseColumnFixMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Removed blocking admin initialization; now handled by AdminInitializationHostedService

            app.Run();
        }
    }
}
