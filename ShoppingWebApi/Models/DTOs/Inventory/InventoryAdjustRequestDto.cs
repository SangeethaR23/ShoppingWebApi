namespace ShoppingWebApi.Models.DTOs.Inventory
{
    public class InventoryAdjustRequestDto
    {
        /// <summary>Positive to add stock, negative to reduce.</summary>
        public int Delta { get; set; }

        /// <summary>Optional reason note for audit trail.</summary>
        public string? Reason { get; set; }
    }
}