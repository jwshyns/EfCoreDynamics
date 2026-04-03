namespace EfCore.Dynamics.FunctionalTests.TestModels.Northwind;

public class OrderDetail : IComparable<OrderDetail>
{
    public Guid OrderDetailID { get; set; }
    public Guid OrderID { get; set; }
    public Guid ProductID { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public float Discount { get; set; }

    public virtual Product Product { get; set; }
    public virtual Order Order { get; set; }

    protected bool Equals(OrderDetail other)
        => OrderID == other.OrderID
           && ProductID == other.ProductID;

    public override bool Equals(object obj)
    {
        if (obj is null) return false;

        return ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((OrderDetail)obj);
    }

    public override int GetHashCode() => HashCode.Combine(OrderID, ProductID);

    public int CompareTo(OrderDetail other)
    {
        if (other == null) return 1;

        var comp1 = OrderID.CompareTo(other.OrderID);
        return comp1 == 0
            ? ProductID.CompareTo(other.ProductID)
            : comp1;
    }
}