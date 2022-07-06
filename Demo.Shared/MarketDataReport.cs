using QuickFix.Fields.Converters;
using System.Text;

namespace Demo.Shared
{
    public class MarketDataReport
    {
        public MarketDataReport(string symbol, decimal price, decimal quantity)
        {
            Symbol = symbol;
            Price = price;
            Quantity = quantity;
            Created = DateTime.UtcNow;
            Sent = false;
        }

        public static MarketDataReport Parse(byte[] data)
        {
            var message = Encoding.Default.GetString(data);
            var parts = message.Split('|');
            var m = new MarketDataReport(parts[0], Convert.ToDecimal(parts[1]), Convert.ToDecimal(parts[2]));
            m.Created = DateTimeConverter.ConvertToDateTime(parts[3], TimeStampPrecision.Microsecond);
            m.OperationId = parts[4];
            m.DependencyId = parts[5];
            return m;
        }

        public string GetFormattedText()
        {
            return $"Symbol={Symbol}, Price={Price:c} Volume={Quantity}";
        }

        public string Symbol { get; set; }
        public decimal Price { get; }
        public decimal Quantity { get; set; }
        public DateTime? Created { get; set; }
        public string? OperationId { get; set; }
        public string? DependencyId { get; set; }
        public bool Sent { get; set; }

        private string GetMessage()
        {
            var createdString = Created.HasValue ? DateTimeConverter.Convert(Created.Value, TimeStampPrecision.Microsecond) : "";
            return $"{Symbol}|{Price}|{Quantity}|{createdString}|{OperationId}|{DependencyId}";
        }

        public byte[] GetBytes()
        {
            return Encoding.Default.GetBytes(GetMessage());
        }

        public override string ToString()
        {
            return GetMessage();
        }
    }
}
