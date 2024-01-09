using CatConsult.AppConfigConfigurationProvider;

using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAppConfig();

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddOptions<YamlTest>()
    .BindConfiguration("YamlTest");

builder.Services.AddFeatureManagement();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();

public class YamlTest
{
    public bool Test { get; set; }

    public Person Person { get; set; } = new();
}

public class Person
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}