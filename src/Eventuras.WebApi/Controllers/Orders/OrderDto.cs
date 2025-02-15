using System;
using System.Linq;
using Eventuras.Domain;
using Eventuras.WebApi.Controllers.Users;
using Newtonsoft.Json;
using NodaTime;

namespace Eventuras.WebApi.Controllers.Orders
{
    public class OrderDto
    {
        public int OrderId { get; set; }

        public Order.OrderStatus Status { get; set; }

        public Instant Time { get; set; }

        public string UserId { get; set; }

        public int RegistrationId { get; set; }

        public OrderLineDto[] Items { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OrderRegistrationDto Registration { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public UserDto User { get; set; }

        public OrderDto()
        {
        }

        public OrderDto(Order order)
        {
            OrderId = order.OrderId;
            Status = order.Status;
            Time = order.OrderTime;
            UserId = order.UserId;
            RegistrationId = order.RegistrationId;

            if (order.Registration != null)
            {
                Registration = new OrderRegistrationDto(order.Registration);
            }

            if (order.User != null)
            {
                User = new UserDto(order.User);
            }

            Items = order.OrderLines?
                .Select(l => new OrderLineDto(l))
                .ToArray();
        }
    }
}
