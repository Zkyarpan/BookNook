namespace BookNook.Models
{
    public class BookWithDiscountViewModel
    {
        // Use constructor to initialize required properties
        public BookWithDiscountViewModel()
        {
            Reviews = new List<Review>();
            // Book must be initialized where the model is created
        }

        public Book Book { get; set; } = null!;
        public bool OnSaleFlag { get; set; }
        public bool IsDiscountActive { get; set; }
        public decimal DiscountedPrice { get; set; }
        public List<Review> Reviews { get; set; }
    }
}