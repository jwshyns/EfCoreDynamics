using System;

namespace EfCore.Dynamics365.Tests.Fixtures;
// ── Core test entities ────────────────────────────────────────────────────

public class Account
{
    public Guid AccountId { get; set; }
    public string? Name { get; set; }
    public decimal? Revenue { get; set; }
    public int? NumberOfEmployees { get; set; }
    public string? EMailAddress1 { get; set; }
}

public class Contact
{
    public Guid ContactId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

// ── Entities for pluralisation-convention coverage ────────────────────────
// These have no Dynamics annotations — tests verify the convention fallbacks.

public class Category
{
    public Guid CategoryId { get; set; }
} // y → ies

public class Box
{
    public Guid BoxId { get; set; }
} // x → xes

public class Church
{
    public Guid ChurchId { get; set; }
} // ch → ches

public class Dish
{
    public Guid DishId { get; set; }
} // sh → shes

public class Status
{
    public Guid StatusId { get; set; }
} // s → ses