using System;
using BookNook.Models;

namespace BookHive.Models
{
    public class Order
    {
        /* ─────────────  book & user  ───────────── */
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;

        /* ─────────────  dates  ───────────── */
        private DateTime _orderDate = DateTime.UtcNow;
        public DateTime OrderDate
        {
            get => _orderDate;
            set => _orderDate = value.Kind == DateTimeKind.Utc
                                    ? value
                                    : value.ToUniversalTime();
        }

        private DateTime? _cancelledAt;
        public DateTime? CancelledAt
        {
            get => _cancelledAt;
            set => _cancelledAt = value is null
                                     ? null
                                     : (value.Value.Kind == DateTimeKind.Utc
                                           ? value.Value
                                           : value.Value.ToUniversalTime());
        }

        private DateTime? _fulfilledAt;
        public DateTime? FulfilledAt
        {
            get => _fulfilledAt;
            set => _fulfilledAt = value is null
                                     ? null
                                     : (value.Value.Kind == DateTimeKind.Utc
                                           ? value.Value
                                           : value.Value.ToUniversalTime());
        }

        /* ─────────────  price / qty / status  ───────────── */
        public decimal TotalPrice { get; set; }
        public int Quantity { get; set; }

        public string ClaimCode { get; set; } = string.Empty;   //  e.g. “AB12‑CD34”
        public string Status { get; set; } = "Placed";       //  “Placed”, “Received”, “Cancelled” …

        public bool IsCancelled { get; set; }
        public bool IsFulfilled { get; set; }

        /* ─────────────  helpers  ───────────── */
        /// <summary> True while the order is still inside its 24‑hour “grace period”. </summary>
        public bool IsCancellable
            => !IsCancelled
               && !IsFulfilled
               && Status != "Received"
               && (DateTime.UtcNow - OrderDate) <= TimeSpan.FromHours(24);
    }
}
