using CognitiveOverloadLMS.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Register MongoDB Service
builder.Services.AddSingleton<MongoDBService>();

// Register MongoDB collections
builder.Services.AddScoped<IMongoCollection<CognitiveOverloadLMS.Models.UserSession>>(serviceProvider =>
{
    var mongoService = serviceProvider.GetRequiredService<MongoDBService>();
    return mongoService.GetCollection<CognitiveOverloadLMS.Models.UserSession>("UserSessions");
});

builder.Services.AddScoped<IMongoCollection<CognitiveOverloadLMS.Models.GameResult>>(serviceProvider =>
{
    var mongoService = serviceProvider.GetRequiredService<MongoDBService>();
    return mongoService.GetCollection<CognitiveOverloadLMS.Models.GameResult>("GameResults");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Configure routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Add specific route for Questions
app.MapControllerRoute(
    name: "questions",
    pattern: "Questions/{action=Section1}/{id?}",
    defaults: new { controller = "Questions" });

builder.Services.AddHttpClient();

app.Run();