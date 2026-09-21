using static PlaywrightAutomationFramework.Tests.Ui.TestingPlaywrightAPICapabilities;

namespace PlaywrightAutomationFramework.Model
{
    public class ProductData
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string price { get; set; } = string.Empty;
        public string is_location_offer { get; set; } = string.Empty;
        public string is_rental { get; set; } = string.Empty;
        public string co2_rating { get; set; } = string.Empty;
        public string in_stock { get; set; } = string.Empty;
        public string is_eco_friendly { get; set; } = string.Empty;
        public string product_image { get; set; } = string.Empty;
        public BrandItem category { get; set; }
        public BrandData brand { get; set; }
    }
}
