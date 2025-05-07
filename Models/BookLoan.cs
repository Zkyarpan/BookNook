namespace BookNook.Models
{
    public class BookLoan
    {
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int BookId { get; set; }
        public Book Book { get; set; }

        private DateTime _loanDate;
        public DateTime LoanDate
        {
            get => _loanDate;
            set => _loanDate = EnsureUtc(value);
        }

        private DateTime? _returnDate;
        public DateTime? ReturnDate
        {
            get => _returnDate;
            set => _returnDate = value.HasValue ? EnsureUtc(value.Value) : null;
        }

        private DateTime _dueDate;
        public DateTime DueDate
        {
            get => _dueDate;
            set => _dueDate = EnsureUtc(value);
        }

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt;
    }
}
