namespace EfCore.Dynamics.FunctionalTests.TestModels.Northwind;

public class Order
{
    public Guid OrderID { get; set; }
    public Guid CustomerID { get; set; }
    public Guid? EmployeeID { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public int? ShipVia { get; set; }
    public decimal? Freight { get; set; }
    public string ShipName { get; set; }
    public string ShipAddress { get; set; }
    public string ShipCity { get; set; }
    public string ShipRegion { get; set; }
    public string ShipPostalCode { get; set; }
    public string ShipCountry { get; set; }

    public Customer Customer { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; }

    protected bool Equals(Order other) => OrderID == other.OrderID;

    public override bool Equals(object obj)
    {
        if (obj is null) return false;

        return ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Order)obj);
    }

    public override int GetHashCode() => OrderID.GetHashCode();

    public override string ToString() => "Order " + OrderID;
}