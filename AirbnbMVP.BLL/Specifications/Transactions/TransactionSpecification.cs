using AirbnbMVP.DAL;
using AirbnbMVP.DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Specifications.Transactions
{
    public class TransactionSpecification : Specification<Transaction>
    {

        public TransactionSpecification(Guid? bookingId, TransactionType? type, TransactionStatus? status) :
            base(t => (!bookingId.HasValue || t.BookingId == bookingId)
            && (!type.HasValue || t.Type == type)
            && (!status.HasValue || t.Status == status) ) {

        
        }
        public Guid? BookingId {  get; set; }
        public TransactionType? Type { get; set; }

        public TransactionStatus? Status {  get; set; }
    }
    
}
