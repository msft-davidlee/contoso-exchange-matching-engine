namespace Demo.Shared
{
    [Serializable]
    public class OrderMessage
    {
        public bool IsBuy { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime Created { get; set; }
        public int OrderId { get; set; }
        public string DependencyId { get; set; }
        public string OperationId { get; set; }
        public string Symbol { get; set; }
        public bool? IsOrderAccepted { get; set; }

        public override string ToString()
        {
            return $"IsBuy:{IsBuy},Quantity:{Quantity},Price:{Price}";
        }
    }
}
