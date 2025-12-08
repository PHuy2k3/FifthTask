// be/Store.Data.Model/Cart.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Store.Data.Model
{
    public class Cart
    {
        [Key]
        public int CartId { get; set; }
        public int UserId { get; set; } // FK to Users.Id
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual List<CartItem> Items { get; set; } = new();
    }
}
