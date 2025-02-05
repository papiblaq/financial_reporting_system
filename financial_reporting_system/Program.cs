using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;
using syncfusion_grid;
using syncfusion_grid.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Check if the environment is set to Production
var environment = builder.Environment.EnvironmentName;
Console.WriteLine($"Current Environment: {environment}");  // Outputs "Production" if correctly set

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register OracleService
builder.Services.AddScoped<OracleService>();

// Add session services
builder.Services.AddDistributedMemoryCache(); // Store session in memory (use other options for production)
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true; // Ensure session cookie is only accessible via HTTP
    options.Cookie.IsEssential = true; // Make the session cookie essential
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
});

// Load the external configuration file
var externalConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("connectionStrings.json", optional: false, reloadOnChange: true)
    .Build();

// Merge the external configuration with the main configuration
builder.Configuration.AddConfiguration(externalConfig);

var app = builder.Build();

// syncfusion licensing 
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NDaF5cWGNCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZcdnRURWVfUkZ3VkI=");

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

// Enable session middleware before other middleware like UseAuthorization
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

namespace syncfusion_grid
{
    class OracleService
    {
    }
}
