using AirbnbMVP.Models.Bookings;
using AirbnbMVP.Models.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.DAL.Models
{
    public class Transaction : BaseEntity
    {
        public Guid BookingId { get; set; }
        public Guid PayerId { get; set; }
        public Guid PayeeId { get; set; }
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }

        public TransactionType Type {  get; set; }
        public string? PaymentReference { get; set; } 
        public DateTime ProcessedAt { get; set; }

        public Booking Booking { get; set; } = null!;
        public User Payer { get; set; } = null!;
        public User Payee { get; set; } = null!;
    }

    
    public enum TransactionType
    {
        Payment,
        Refund,
        Payout
    }
    public enum TransactionStatus
    {
        Pending,
        Completed,
        Failed
    }
}
