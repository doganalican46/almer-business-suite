using Almer.Domain;

namespace Almer.Application;

public static class InventoryRules
{
    public static decimal ToDelta(StockMovementType type, decimal quantity, decimal currentStock)
    {
        if (quantity < 0) throw new ArgumentException("Quantity cannot be negative.");
        var delta = type switch
        {
            StockMovementType.Opening or StockMovementType.Purchase or StockMovementType.CustomerReturn or StockMovementType.TransferIn => quantity,
            StockMovementType.Sale or StockMovementType.SupplierReturn or StockMovementType.Waste or StockMovementType.TransferOut => -quantity,
            StockMovementType.CountAdjustment => quantity - currentStock,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        if (currentStock + delta < 0) throw new InvalidOperationException("Insufficient stock.");
        return delta;
    }
}
