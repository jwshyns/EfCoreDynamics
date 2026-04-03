namespace EfCore.Dynamics.FunctionalTests.TestModels.Northwind;

public class Product
{
    public Product()
    {
        OrderDetails = new List<OrderDetail>();
    }

    public Guid ProductID { get; set; }
    public string ProductName { get; set; }
    public int? SupplierID { get; set; }
    public int? CategoryID { get; set; }
    public string QuantityPerUnit { get; set; }
    public decimal? UnitPrice { get; set; }
    public int UnitsInStock { get; set; }
    public int? UnitsOnOrder { get; set; }
    public int? ReorderLevel { get; set; }
    public bool Discontinued { get; set; }

    public virtual List<OrderDetail> OrderDetails { get; set; }

    protected bool Equals(Product other) => Equals(ProductID, other.ProductID);

    public override bool Equals(object obj)
    {
        if (obj is null) return false;

        return ReferenceEquals(this, obj) || obj.GetType() == GetType()
            && Equals((Product)obj);
    }

    public override int GetHashCode() => ProductID.GetHashCode();

    public override string ToString() => "Product " + ProductID;
}