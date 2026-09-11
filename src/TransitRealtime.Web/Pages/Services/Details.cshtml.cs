using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Models;
using TransitRealtime.Web.Services;

namespace TransitRealtime.Web.Pages.Services;

public class DetailsModel(ApplicationDbContext db, ApiKeyService apiKeys, ConnectionTracker tracker) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public TransitService Service { get; set; } = null!;
    public List<AuditLogEntry> RecentLogs { get; set; } = [];
    public int ConnectedClients { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string WsUrl { get; set; } = string.Empty;
    public string? NewApiKey { get; set; }
    public string? NewSubscriberSecret { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var service = await db.TransitServices
            .Include(s => s.ApiKeys)
            .FirstOrDefaultAsync(s => s.Id == Id);
        if (service is null)
            return NotFound();

        Service = service;
        RecentLogs = await db.AuditLogs
            .Where(l => l.ServiceId == Id)
            .OrderByDescending(l => l.Id) // Id is insertion-ordered; SQLite can't ORDER BY DateTimeOffset server-side
            .Take(20)
            .ToListAsync();
        ConnectedClients = tracker.GetCount(Id.ToString());
        NewApiKey = TempData["NewApiKey"] as string;
        NewSubscriberSecret = TempData["NewSubscriberSecret"] as string;

        WebhookUrl = $"{Request.Scheme}://{Request.Host}/webhook/{Id}";
        var wsScheme = Request.Scheme == "https" ? "wss" : "ws";
        WsUrl = $"{wsScheme}://{Request.Host}/hub";

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateKeyAsync()
    {
        var generated = await apiKeys.CreateApiKeyAsync(Id);
        db.AuditLogs.Add(new AuditLogEntry { ServiceId = Id, EventType = AuditEventType.ApiKeyCreated });
        await db.SaveChangesAsync();
        TempData["NewApiKey"] = generated.RawKey;
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostRevokeKeyAsync(Guid keyId)
    {
        await apiKeys.RevokeApiKeyAsync(keyId);
        db.AuditLogs.Add(new AuditLogEntry { ServiceId = Id, EventType = AuditEventType.ApiKeyRevoked });
        await db.SaveChangesAsync();
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync()
    {
        var service = await db.TransitServices.FindAsync(Id);
        if (service is not null)
        {
            service.IsActive = !service.IsActive;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostRegenerateSubscriberSecretAsync()
    {
        var service = await db.TransitServices.FindAsync(Id);
        if (service is not null)
        {
            service.SubscriberSecret = SecretGenerator.Generate();
            await db.SaveChangesAsync();
            TempData["NewSubscriberSecret"] = service.SubscriberSecret;
        }
        return RedirectToPage(new { id = Id });
    }
}
