// ─────────────────────────────────────────────────────────────────────────────
// EfCore.Dynamics365 — usage sample
// Replace the placeholder values with your real Dynamics 365 / Dataverse credentials.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using System.Threading.Tasks;
using EfCore.Dynamics365;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore;

// ── 1. Define your entity classes ────────────────────────────────────────────

/// <summary>Mirrors the Dynamics 365 "account" entity.</summary>
public class Account
{
    public Guid AccountId { get; set; }
    public string? Name { get; set; }
    public string? Telephone1 { get; set; }
    public string? EMailAddress1 { get; set; }
    public int? NumberOfEmployees { get; set; }
    public DateTimeOffset? CreatedOn { get; set; }
}

/// <summary>Mirrors the Dynamics 365 "contact" entity.</summary>
public class Contact
{
    public Guid ContactId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? EMailAddress1 { get; set; }
    public Guid? ParentCustomerId { get; set; }
}

// ── 2. Define your DbContext ──────────────────────────────────────────────────

public class CrmContext : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Contact> Contacts { get; set; } = null!;

    public CrmContext(DbContextOptions<CrmContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(b =>
        {
            b.HasKey(e => e.AccountId);
            b.HasEntitySetName("accounts");
            b.HasDynamicsLogicalName("account");

            b.Property(e => e.AccountId)
                .HasAttributeLogicalName("accountid")
                .ValueGeneratedOnAdd();

            b.Property(e => e.Name)
                .HasAttributeLogicalName("name");

            b.Property(e => e.Telephone1)
                .HasAttributeLogicalName("telephone1");

            b.Property(e => e.EMailAddress1)
                .HasAttributeLogicalName("emailaddress1");

            b.Property(e => e.NumberOfEmployees)
                .HasAttributeLogicalName("numberofemployees");

            b.Property(e => e.CreatedOn)
                .HasAttributeLogicalName("createdon")
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Contact>(b =>
        {
            b.HasKey(e => e.ContactId);
            b.HasEntitySetName("contacts");
            b.HasDynamicsLogicalName("contact");

            b.Property(e => e.ContactId)
                .HasAttributeLogicalName("contactid")
                .ValueGeneratedOnAdd();

            b.Property(e => e.FirstName)
                .HasAttributeLogicalName("firstname");

            b.Property(e => e.LastName)
                .HasAttributeLogicalName("lastname");

            b.Property(e => e.EMailAddress1)
                .HasAttributeLogicalName("emailaddress1");

            b.Property(e => e.ParentCustomerId)
                .HasAttributeLogicalName("_parentcustomerid_value");
        });
    }
}

// ── 3. Wire up and run ────────────────────────────────────────────────────────

class Program
{
    static async Task Main()
    {
        var options = new DbContextOptionsBuilder<CrmContext>()
            .UseDynamics365(
                serviceUrl:   "https://yourorg.api.crm.dynamics.com",
                tenantId:     "00000000-0000-0000-0000-000000000000",
                clientId:     "00000000-0000-0000-0000-000000000000",
                clientSecret: "your-client-secret")
            .Options;

        await using var ctx = new CrmContext(options);

        // ── Read ───────────────────────────────────────────────────────────────

        Console.WriteLine("── All accounts ──────────────────────────────");
        var accounts = await ctx.Accounts
            .Where(a => a.Name != null)
            .OrderBy(a => a.Name)
            .Take(10)
            .ToListAsync();

        foreach (var a in accounts)
            Console.WriteLine($"  [{a.AccountId}] {a.Name}");

        // ── Create ─────────────────────────────────────────────────────────────

        Console.WriteLine("\n── Create account ────────────────────────────");
        var newAccount = new Account
        {
            Name             = "Contoso Ltd.",
            Telephone1       = "+1 555-0100",
            EMailAddress1    = "info@contoso.com",
            NumberOfEmployees = 250,
        };
        ctx.Accounts.Add(newAccount);
        await ctx.SaveChangesAsync();
        Console.WriteLine($"  Created: {newAccount.AccountId}");

        // ── Update ─────────────────────────────────────────────────────────────

        Console.WriteLine("\n── Update account ────────────────────────────");
        newAccount.NumberOfEmployees = 300;
        await ctx.SaveChangesAsync();
        Console.WriteLine("  Updated employee count to 300.");

        // ── Delete ─────────────────────────────────────────────────────────────

        Console.WriteLine("\n── Delete account ────────────────────────────");
        ctx.Accounts.Remove(newAccount);
        await ctx.SaveChangesAsync();
        Console.WriteLine("  Deleted.");
    }
}
