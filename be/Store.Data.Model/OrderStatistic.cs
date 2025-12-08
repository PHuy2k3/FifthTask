using System;

namespace Store.Data.Model
{
    public class OrderStatistic
    {
        public DateTime StatDate { get; set; }   // PK
        public int OrdersCount { get; set; }
        public decimal Revenue { get; set; }
        public DateTime RefreshedAt { get; set; }
    }
}
