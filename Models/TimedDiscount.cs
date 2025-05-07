namespace BookNook.Models
{
    public class TimedDiscount
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public Book Book { get; set; }
        public decimal DiscountPercentage { get; set; }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => _startDate = EnsureUtc(value);
        }

        private DateTime _expiresAt;
        public DateTime ExpiresAt
        {
            get => _expiresAt;
            set => _expiresAt = EnsureUtc(value);
        }

        public bool OnSaleFlag { get; set; }

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt;
    }
}
