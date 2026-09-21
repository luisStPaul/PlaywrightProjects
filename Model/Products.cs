using static PlaywrightAutomationFramework.Tests.Ui.TestingPlaywrightAPICapabilities;

namespace PlaywrightAutomationFramework.Model
{
    public class Products
    {
        public int current_page { get; set; }
        public List<ProductData> data { get; set; }
        public int from { get; set; }
        public int last_page { get; set; }
        public int per_page { get; set; }
        public int to { get; set; }
        public int total { get; set; }
    }
}
