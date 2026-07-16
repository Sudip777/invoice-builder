using Microsoft.EntityFrameworkCore;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;

namespace InvoiceBuilder.Services;

public class SenderService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<Sender>> GetAllAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.Senders.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Sender?> GetAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        return await db.Senders.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> CreateAsync(Sender sender)
    {
        await using var db = factory.CreateDbContext();
        db.Senders.Add(sender);
        await db.SaveChangesAsync();
        return sender.Id;
    }

    public async Task UpdateAsync(Sender sender)
    {
        await using var db = factory.CreateDbContext();
        db.Senders.Update(sender);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        var sender = await db.Senders.FindAsync(id);
        if (sender is not null)
        {
            db.Senders.Remove(sender);
            await db.SaveChangesAsync();
        }
    }
}
