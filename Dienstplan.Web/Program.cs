using Dienstplan.Web.Calendar;
using Dienstplan.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseAntiforgery();

// The calendar file must come from a real URL with "text/calendar" (not a JS blob download),
// otherwise iOS Safari just stores it in "Downloads" instead of offering "Add all to calendar".
app.MapGet("/dienstplan.ics", (string? p, HttpContext context) =>
{
    if (!ShiftPlan.TryDecode(p, out var events))
        return Results.BadRequest("Der Dienstplan konnte nicht gelesen werden.");

    var fileName = $"dienstplan-{events[0].Day:yyyy-MM}.ics";
    context.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
    return Results.Text(IcsWriter.Write(events, DateTime.UtcNow), "text/calendar; charset=utf-8");
});

app.MapRazorComponents<App>();

app.Run();
