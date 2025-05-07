namespace BookNook.Models
{
    public class Review
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int BookId { get; set; }
        public Book Book { get; set; }

        public int? ParentReviewId { get; set; }
        public Review ParentReview { get; set; }
        public ICollection<Review> Replies { get; set; } = new List<Review>();

        private DateTime _reviewDate;
        public DateTime ReviewDate
        {
            get => _reviewDate;
            set => _reviewDate = EnsureUtc(value);
        }

        public string Comment { get; set; }
        public int Rating { get; set; }

        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt;
    }
}
