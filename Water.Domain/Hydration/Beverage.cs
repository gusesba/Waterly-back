namespace Water.Domain.Hydration;

public sealed class Beverage
{
    private Beverage()
    {
    }

    public Beverage(string code, decimal hydrationFactor, int sortOrder)
    {
        Code = code;
        HydrationFactor = hydrationFactor;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public decimal HydrationFactor { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
}
