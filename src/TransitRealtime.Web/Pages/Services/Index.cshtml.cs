using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Models;
using TransitRealtime.Web.Services;

namespace TransitRealtime.Web.Pages.Services;

public class IndexModel(ApplicationDbContext db, ConnectionTracker tracker) : PageModel
{
    public List<ServiceRow> Rows { get; set; } = [];

    public record ServiceRow(TransitService Service, int ActiveKeyCount, int ConnectedClients);

    public async Task OnGetAsync()
    {
        var services = await db.TransitServices
            .Include(s => s.ApiKeys)
            .ToListAsync();

        Rows = services
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ServiceRow(
                s,
                s.ApiKeys.Count(k => k.IsActive),
                tracker.GetCount(s.Id.ToString())))
            .ToList();
    }
}
