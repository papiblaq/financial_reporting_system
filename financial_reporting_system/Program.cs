using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using syncfusion_grid;
using syncfusion_grid.Controllers;
//using static financial_reporting_system.Statement_typesController;
using static syncfusion_grid.Controllers.MappingController;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register OracleService
builder.Services.AddScoped<OracleService>();


var app = builder.Build();


// syncfusion liscensing 

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Mgo+DSMBPh8sVXJyS0d+X1RPd11dXmJWd1p/THNYflR1fV9DaUwxOX1dQl9nSXlSc0ViWHhecnRVQWc=");


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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Statement_types}/{action=Index}/{id?}");

app.Run();

namespace syncfusion_grid
{
    class OracleService
    {
    }
}
