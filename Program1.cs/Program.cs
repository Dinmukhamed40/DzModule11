using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceUML_CSharp
{
    #region Enums
    public enum UserRole { Client, Administrator }
    public enum OrderStatus { Created, Processing, InDelivery, Delivered, Cancelled }
    public enum PaymentType { Card, EWallet, BankTransfer }
    public enum PaymentStatus { Pending, Completed, Failed, Refunded }
    public enum DeliveryStatus { Pending, Shipped, InTransit, Delivered, Returned }
    #endregion

    #region Abstract User and Derived Classes
    // Абстрактная сущность User: демонстрирует требование "минимум одна абстрактная сущность".
    public abstract class User
    {
        public int Id { get; protected set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public UserRole Role { get; protected set; }

        public User(int id, string name, string email, string address, string phone, UserRole role)
        {
            Id = id;
            Name = name;
            Email = email;
            Address = address;
            Phone = phone;
            Role = role;
        }

        public virtual void Register()
        {
            // Регистрация: в реальном приложении — в БД, проверка, валидация.
            Console.WriteLine($"{Role} {Name} зарегистрирован (email: {Email}).");
        }

        public virtual bool Login(string email, string password)
        {
            // Заглушка авторизации
            Console.WriteLine($"{Role} {Name} попытка входа с {email}.");
            return true;
        }

        public virtual void UpdateProfile(string name, string address, string phone)
        {
            Name = name; Address = address; Phone = phone;
            Console.WriteLine($"{Role} {Name} обновил профиль.");
        }
    }

    public class Client : User
    {
        public int LoyaltyPoints { get; private set; } = 0;
        public List<Order> Orders { get; } = new List<Order>();

        public Client(int id, string name, string email, string address, string phone)
            : base(id, name, email, address, phone, UserRole.Client) { }

        public void AddPoints(int points)
        {
            LoyaltyPoints += points;
            Console.WriteLine($"Клиент {Name}: начислено {points} баллов. Всего: {LoyaltyPoints}");
        }

        public IEnumerable<Order> ViewOrderHistory() => Orders;
    }

    public class Administrator : User
    {
        private readonly AdminLogger _logger;

        public Administrator(int id, string name, string email, string address, string phone, AdminLogger logger)
            : base(id, name, email, address, phone, UserRole.Administrator)
        {
            _logger = logger;
        }

        public void LogAction(string action)
        {
            _logger.LogAction(Id, action);
        }
    }
    #endregion

    #region Product, Category, Warehouse (multi-warehouse support)
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class Product
    {
        public int Id { get; protected set; }
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public decimal Price { get; protected set; }
        // stock managed via warehouses
        public Category Category { get; set; }

        public Product(int id, string name, string description, decimal price, Category category)
        {
            Id = id; Name = name; Description = description; Price = price; Category = category;
        }

        public virtual void Update(string name, string description, decimal price)
        {
            Name = name; Description = description; Price = price;
        }

        public virtual void Delete()
        {
            // пометка на удаление или удаление из репозитория
            Console.WriteLine($"Товар {Name} (Id={Id}) удалён.");
        }
    }

    public class Warehouse
    {
        public int Id { get; set; }
        public string Location { get; set; }
        // productId -> quantity
        private readonly Dictionary<int, int> _stock = new Dictionary<int, int>();

        public Warehouse(int id, string location)
        {
            Id = id; Location = location;
        }

        public int GetStock(int productId) => _stock.ContainsKey(productId) ? _stock[productId] : 0;

        public void AddStock(int productId, int qty)
        {
            if (_stock.ContainsKey(productId)) _stock[productId] += qty;
            else _stock[productId] = qty;
        }

        public bool ReserveStock(int productId, int qty)
        {
            if (GetStock(productId) >= qty)
            {
                _stock[productId] -= qty;
                return true;
            }
            return false;
        }

        public void ReleaseStock(int productId, int qty)
        {
            AddStock(productId, qty);
        }
    }
    #endregion

    #region Cart and PromoCode
    public class CartItem
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }

        public decimal LineTotal => Product.Price * Quantity;
    }

    public class PromoCode
    {
        public string Code { get; set; }
        public int DiscountPercent { get; set; } = 0;
        public DateTime ExpirationDate { get; set; } = DateTime.MaxValue;

        public bool Validate() => DateTime.UtcNow <= ExpirationDate;
    }

    public class Cart
    {
        public List<CartItem> Items { get; } = new List<CartItem>();
        public PromoCode AppliedPromo { get; private set; }

        public void AddProduct(Product p, int qty = 1)
        {
            var existing = Items.FirstOrDefault(i => i.Product.Id == p.Id);
            if (existing != null) existing.Quantity += qty;
            else Items.Add(new CartItem { Product = p, Quantity = qty });
        }

        public void RemoveProduct(int productId)
        {
            Items.RemoveAll(i => i.Product.Id == productId);
        }

        public decimal CalculateTotal()
        {
            var total = Items.Sum(i => i.LineTotal);
            if (AppliedPromo != null && AppliedPromo.Validate())
            {
                total = total * (100 - AppliedPromo.DiscountPercent) / 100m;
            }
            return total;
        }

        public bool ApplyPromo(PromoCode promo)
        {
            if (promo == null || !promo.Validate()) return false;
            AppliedPromo = promo;
            return true;
        }
    }
    #endregion

    #region Orders
    public class OrderItem
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtPurchase { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public OrderStatus Status { get; set; }
        public Client Client { get; set; }
        public List<OrderItem> Items { get; } = new List<OrderItem>();
        public decimal TotalAmount { get; set; }
        public Delivery Delivery { get; set; }
        public Payment Payment { get; set; }
        public PromoCode AppliedPromo { get; set; }

        public Order(int id, Client client)
        {
            Id = id; Client = client; CreatedDate = DateTime.UtcNow; Status = OrderStatus.Created;
        }

        public void Place()
        {
            // бизнес-логика оформления: резерв склада, создание платежа и т.д.
            Status = OrderStatus.Processing;
            Console.WriteLine($"Order {Id} placed for client {Client.Name}. Status: {Status}");
        }

        public void Cancel()
        {
            Status = OrderStatus.Cancelled;
            // высвободить резерв, инициировать возврат и т.д.
            Console.WriteLine($"Order {Id} cancelled.");
        }

        public void Pay(IPaymentGateway paymentGateway)
        {
            if (Payment == null) throw new InvalidOperationException("Payment object is missing.");
            var result = paymentGateway.ProcessPayment(Payment);
            if (result)
            {
                Payment.Status = PaymentStatus.Completed;
                Console.WriteLine($"Order {Id}: payment completed.");
            }
            else
            {
                Payment.Status = PaymentStatus.Failed;
                Console.WriteLine($"Order {Id}: payment failed.");
            }
        }
    }
    #endregion

    #region Payment and Delivery
    public class Payment
    {
        public int Id { get; set; }
        public PaymentType Type { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string ExternalTransactionId { get; set; }

        public bool Process(IPaymentGateway gateway)
        {
            return gateway.ProcessPayment(this);
        }

        public bool Refund(IPaymentGateway gateway)
        {
            var res = gateway.RefundPayment(this);
            if (res) Status = PaymentStatus.Refunded;
            return res;
        }
    }

    public class Delivery
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public Courier Courier { get; set; }
        public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
        public string TrackingNumber { get; set; }

        public void Send(ICourierIntegration courierIntegration)
        {
            var tn = courierIntegration.CreateShipment(this);
            if (!string.IsNullOrEmpty(tn))
            {
                TrackingNumber = tn;
                Status = DeliveryStatus.Shipped;
                Console.WriteLine($"Delivery {Id} shipped. Tracking: {TrackingNumber}");
            }
        }

        public void Track(ICourierIntegration courierIntegration)
        {
            var s = courierIntegration.GetTrackingStatus(TrackingNumber);
            Console.WriteLine($"Delivery {Id} tracking: {s}");
        }

        public void Complete()
        {
            Status = DeliveryStatus.Delivered;
            Console.WriteLine($"Delivery {Id} completed.");
        }
    }

    public class Courier
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Company { get; set; }
    }
    #endregion

    #region Reviews & Ratings
    public class Review
    {
        public int Id { get; set; }
        public Client Author { get; set; }
        public Product Product { get; set; }
        public string Text { get; set; }
        public int Rating { get; set; } // 1..5
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RatingAggregate
    {
        // пример агрегирования рейтингов
        public int ProductId { get; set; }
        public double AverageRating { get; set; }
        public int Count { get; set; }
    }
    #endregion

    #region Interfaces for External Systems (payments, couriers)
    public interface IPaymentGateway
    {
        // Возвращает true при успешной оплате
        bool ProcessPayment(Payment payment);
        bool RefundPayment(Payment payment);
    }

    public interface ICourierIntegration
    {
        // Создаёт отправку и возвращает tracking number
        string CreateShipment(Delivery delivery);
        // Получить статус по tracking number
        string GetTrackingStatus(string trackingNumber);
    }

    // Примеры простых stub-реализаций
    public class DummyPaymentGateway : IPaymentGateway
    {
        public bool ProcessPayment(Payment payment)
        {
            Console.WriteLine($"[DummyPayment] Processing payment {payment.Id} amount {payment.Amount}");
            payment.ExternalTransactionId = $"TXN-{Guid.NewGuid()}";
            return true;
        }

        public bool RefundPayment(Payment payment)
        {
            Console.WriteLine($"[DummyPayment] Refunding payment {payment.Id}");
            return true;
        }
    }

    public class DummyCourierIntegration : ICourierIntegration
    {
        public string CreateShipment(Delivery delivery)
        {
            Console.WriteLine($"[DummyCourier] Creating shipment to {delivery.Address}");
            return $"TRK-{Guid.NewGuid().ToString().Split('-')[0]}";
        }

        public string GetTrackingStatus(string trackingNumber)
        {
            return "InTransit";
        }
    }
    #endregion

    #region Services: Inventory, Order, Loyalty, Logger
    public class InventoryService
    {
        private readonly List<Warehouse> _warehouses;

        public InventoryService(List<Warehouse> warehouses)
        {
            _warehouses = warehouses;
        }

        // Общее количество товара по всем складам
        public int GetTotalStock(int productId) => _warehouses.Sum(w => w.GetStock(productId));

        // Пытаемся зарезервировать из всех складов (простой greedy)
        public bool ReserveStock(int productId, int qty)
        {
            var remaining = qty;
            foreach (var wh in _warehouses)
            {
                int available = wh.GetStock(productId);
                if (available <= 0) continue;
                int take = Math.Min(available, remaining);
                if (take > 0)
                {
                    wh.ReserveStock(productId, take);
                    remaining -= take;
                    if (remaining == 0) return true;
                }
            }
            // Если не удалось зарезервировать — откат (для простоты: ничего не делаем)
            return false;
        }

        public void ReleaseStock(int productId, int qty)
        {
            // Для простоты: добавляем на первый склад
            if (_warehouses.Count > 0) _warehouses[0].AddStock(productId, qty);
        }
    }

    public class OrderService
    {
        private readonly InventoryService _inventory;
        private readonly IPaymentGateway _paymentGateway;
        private readonly ICourierIntegration _courierIntegration;

        public OrderService(InventoryService inventory, IPaymentGateway paymentGateway, ICourierIntegration courierIntegration)
        {
            _inventory = inventory;
            _paymentGateway = paymentGateway;
            _courierIntegration = courierIntegration;
        }

        public bool CreateOrder(Order order)
        {
            // Резервируем товары
            foreach (var item in order.Items)
            {
                var reserved = _inventory.ReserveStock(item.Product.Id, item.Quantity);
                if (!reserved)
                {
                    Console.WriteLine($"Не удалось зарезервировать товар {item.Product.Name}");
                    return false;
                }
            }
            order.Place();
            return true;
        }

        public bool ProcessPayment(Order order)
        {
            if (order.Payment == null) throw new InvalidOperationException("Payment missing.");
            order.Pay(_paymentGateway);
            return order.Payment.Status == PaymentStatus.Completed;
        }

        public void ShipOrder(Order order)
        {
            if (order.Delivery == null) throw new InvalidOperationException("Delivery missing.");
            order.Delivery.Send(_courierIntegration);
            order.Status = OrderStatus.InDelivery;
        }
    }

    public class LoyaltyService
    {
        public void AddLoyaltyPoints(Client client, decimal orderAmount)
        {
            int points = (int)Math.Floor(orderAmount / 10); // 1 балл за каждые 10 единиц валюты
            client.AddPoints(points);
        }
    }

    public class AdminLogger
    {
        public void LogAction(int adminId, string action)
        {
            // В реальном проекте: записывать в БД или файл
            Console.WriteLine($"[AdminLog] AdminId={adminId} Action='{action}' Time={DateTime.UtcNow}");
        }
    }
    #endregion

    #region Factory pattern for Product creation
    // Простой пример фабрики продуктов: можно расширять для разных типов товаров (например: DigitalProduct, PhysicalProduct).
    public interface IProductFactory
    {
        Product CreateProduct(int id, string name, string description, decimal price, Category category);
    }

    public class ProductFactory : IProductFactory
    {
        public Product CreateProduct(int id, string name, string description, decimal price, Category category)
        {
            // Здесь можно добавить логику для выбора подкласса Product (напр., в зависимости от категории)
            return new Product(id, name, description, price, category);
        }
    }
    #endregion

    #region Example usage (Main)
    class Program
    {
        static void Main(string[] args)
        {
            // Setup demo data
            var electronics = new Category { Id = 1, Name = "Electronics", Description = "Electronic devices" };
            var prodFactory = new ProductFactory();
            var phone = prodFactory.CreateProduct(101, "Smartphone X", "Flagship phone", 799.99m, electronics);

            var wh1 = new Warehouse(1, "Almaty Warehouse");
            wh1.AddStock(phone.Id, 10);
            var wh2 = new Warehouse(2, "Astana Warehouse");
            wh2.AddStock(phone.Id, 5);

            var inventoryService = new InventoryService(new List<Warehouse> { wh1, wh2 });
            var paymentGateway = new DummyPaymentGateway();
            var courierIntegration = new DummyCourierIntegration();
            var orderService = new OrderService(inventoryService, paymentGateway, courierIntegration);
            var loyaltyService = new LoyaltyService();
            var adminLogger = new AdminLogger();

            // Users
            var client = new Client(1, "Dinmukhamed", "dm@example.com", "Some Address", "+7700");
            var admin = new Administrator(2, "Admin", "admin@example.com", "HQ", "+7701", adminLogger);
            admin.LogAction("Created demo data");

            // Cart -> Order
            var cart = new Cart();
            cart.AddProduct(phone, 2);

            var order = new Order(1001, client);
            order.Items.Add(new OrderItem { Product = phone, Quantity = 2, PriceAtPurchase = phone.Price });
            order.TotalAmount = cart.CalculateTotal();
            order.Payment = new Payment { Id = 5001, Type = PaymentType.Card, Amount = order.TotalAmount };

            // Create order (reserve stock)
            var created = orderService.CreateOrder(order);
            if (!created) return;

            // Process payment
            orderService.ProcessPayment(order);

            // Loyalty points
            loyaltyService.AddLoyaltyPoints(client, order.TotalAmount);

            // Setup delivery and ship
            order.Delivery = new Delivery { Id = 3001, Address = client.Address, Courier = new Courier { Id = 10, Name = "QCourier" } };
            orderService.ShipOrder(order);

            // Track delivery
            order.Delivery.Track(courierIntegration);

            // Admin actions
            admin.LogAction($"Order {order.Id} processed and shipped.");

            Console.WriteLine("Demo finished.");
        }
    }
    #endregion
}
