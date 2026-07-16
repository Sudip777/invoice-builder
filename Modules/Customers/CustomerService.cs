using Microsoft.EntityFrameworkCore;
using InvoiceBuilder.Data;
using InvoiceBuilder.Models;

namespace InvoiceBuilder.Modules.Customers;

public class CustomerService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<Customer>> GetAllAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Customer?> GetAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int> CreateAsync(Customer customer)
    {
        await using var db = factory.CreateDbContext();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    public async Task UpdateAsync(Customer customer)
    {
        await using var db = factory.CreateDbContext();
        db.Customers.Update(customer);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        var customer = await db.Customers.FindAsync(id);
        if (customer is not null)
        {
            db.Customers.Remove(customer);
            await db.SaveChangesAsync();
        }
    }
}
