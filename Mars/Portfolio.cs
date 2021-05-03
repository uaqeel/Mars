using System.Collections.Generic;

namespace Mars
{
    internal class Portfolio
    {
        double currentPortfolioValue;

        double InitialPortfolioValue { get; set; }
        double CurrentPortfolioValue
        {
            get
            {
                UpdatePortfolioValue();
                return currentPortfolioValue;
            }
        }

        Dictionary<string, double> PortfolioAssets { get; set; }        // values = sizes

        public Portfolio(double initialPortfolioValue)
        {
            InitialPortfolioValue = InitialPortfolioValue;

            PortfolioAssets = new Dictionary<string, double>();
        }

        public void UpdatePortfolioValue()
        {
            currentPortfolioValue = 0;
        }
    }
}