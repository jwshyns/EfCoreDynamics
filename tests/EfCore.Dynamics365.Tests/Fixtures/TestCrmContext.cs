using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Dynamics365.Tests.Fixtures;

public class TestCrmContext : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Contact> Contacts { get; set; } = null!;

    public TestCrmContext(DbContextOptions<TestCrmContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(b =>
        {
            b.HasKey(a => a.AccountId);
            b.HasEntitySetName("accounts");
            b.HasDynamicsLogicalName("account");

            b.Property(a => a.AccountId)
                .HasAttributeLogicalName("accountid")
                .ValueGeneratedOnAdd();

            b.Property(a => a.Name)
                .HasAttributeLogicalName("name");

            b.Property(a => a.Revenue)
                .HasAttributeLogicalName("revenue");

            b.Property(a => a.NumberOfEmployees)
                .HasAttributeLogicalName("numberofemployees");

            b.Property(a => a.EMailAddress1)
                .HasAttributeLogicalName("emailaddress1");

            b.HasOne(x => x.Contact)
                .WithMany()
                .HasForeignKey(x => x.AccountId);
        });

        modelBuilder.Entity<Contact>(b =>
        {
            b.HasKey(c => c.ContactId);
            b.HasEntitySetName("contacts");
            b.HasDynamicsLogicalName("contact");

            b.Property(c => c.ContactId)
                .HasAttributeLogicalName("contactid")
                .ValueGeneratedOnAdd();

            b.Property(c => c.FirstName)
                .HasAttributeLogicalName("firstname");

            b.Property(c => c.LastName)
                .HasAttributeLogicalName("lastname");
        });
    }
}