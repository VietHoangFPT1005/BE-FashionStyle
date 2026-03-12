namespace MV.DomainLayer.Configuration
{
    public class SePaySettings
    {
        public string BankName { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string QrBaseUrl { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        public int PaymentExpiryMinutes { get; set; } = 10;
        public string MerchantId { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
    }
}
