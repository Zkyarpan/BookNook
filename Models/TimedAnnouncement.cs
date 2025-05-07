using System;

namespace BookNook.Models                
{
    public class TimedAnnouncement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => _createdAt = EnsureUtc(value);
        }

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

        /* helper --------------------------------------------------------- */
        private static DateTime EnsureUtc(DateTime dt) =>
            dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                _ /* local */          => dt.ToUniversalTime()
            };
    }
}
