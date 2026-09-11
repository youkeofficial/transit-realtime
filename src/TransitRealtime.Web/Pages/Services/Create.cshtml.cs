using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Models;
using TransitRealtime.Web.Services;

namespace TransitRealtime.Web.Pages.Services;

public class CreateModel(ApplicationDbContext db, ApiKeyService apiKeys) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var service = new TransitService
        {
            Name = Input.Name,
            Description = Input.Description,
            CreatedByUserId = User.Identity?.Name,
            SubscriberSecret = SecretGenerator.Generate(),
        };
        db.TransitServices.Add(service);
        await db.SaveChangesAsync();

        var generated = await apiKeys.CreateApiKeyAsync(service.Id);

        db.AuditLogs.Add(new AuditLogEntry
        {
            ServiceId = service.Id,
            EventType = AuditEventType.ServiceCreated,
        });
        await db.SaveChangesAsync();

        TempData["NewApiKey"] = generated.RawKey;
        TempData["NewSubscriberSecret"] = service.SubscriberSecret;
        return RedirectToPage("Details", new { id = service.Id });
    }
}
