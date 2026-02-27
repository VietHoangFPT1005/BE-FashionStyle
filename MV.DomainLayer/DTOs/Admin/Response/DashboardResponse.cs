namespace MV.DomainLayer.DTOs.Admin.Response
{
    public class DashboardResponse
    {
        public DashboardOverview Overview { get; set; } = new();
        public Dictionary<string, int> OrdersByStatus { get; set; } = new();
        public List<DashboardRecentOrder> RecentOrders { get; set; } = new();
        public List<DashboardTopProduct> TopProducts { get; set; } = new();
    }

    public class DashboardOverview
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalProducts { get; set; }
    }

    public class DashboardRecentOrder
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }

    public class DashboardTopProduct
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SoldCount { get; set; }
        public decimal Revenue { get; set; }
    }
}
