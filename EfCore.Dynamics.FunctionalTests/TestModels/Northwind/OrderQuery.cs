namespace EfCore.Dynamics.FunctionalTests.TestModels.Northwind;

public class OrderQuery
{
    public OrderQuery()
    {
    }

    public OrderQuery(Guid customerID)
    {
        CustomerID = customerID;
    }

    public Guid CustomerID { get; set; }

    public Customer Customer { get; set; }

    protected bool Equals(OrderQuery other) => Equals(CustomerID, other.CustomerID);

    public override bool Equals(object obj)
    {
        if (obj is null) return false;

        return ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((OrderQuery)obj);
    }

    public static bool operator ==(OrderQuery left, OrderQuery right) => Equals(left, right);

    public static bool operator !=(OrderQuery left, OrderQuery right) => !Equals(left, right);

    public override int GetHashCode() => CustomerID.GetHashCode();

    public override string ToString() => "OrderView " + CustomerID;
}