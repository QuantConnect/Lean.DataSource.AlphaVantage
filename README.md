![LEAN Data Source SDK](https://github.com/QuantConnect/Lean.DataSource.AlphaVantage/assets/79997186/edaf8bbe-592a-4ac0-9b8a-cf98a591d7a3)

# Lean Alpha Vantage DataSource Plugin

[![Build Status](https://github.com/QuantConnect/LeanDataSdk/workflows/Build%20%26%20Test/badge.svg)](https://github.com/QuantConnect/LeanDataSdk/actions?query=workflow%3A%22Build%20%26%20Test%22) [![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

### Introduction

Welcome to the Alpha Vantage API Connector Library for .NET 6. This open-source project provides a robust and efficient C# library designed to seamlessly connect with the Alpha Vantage API. The library facilitates easy integration with the QuantConnect [LEAN Algorithmic Trading Engine](https://github.com/quantConnect/Lean), offering a clear and straightforward way for users to incorporate Alpha Vantage's extensive financial datasets into their algorithmic trading strategies.

### Alpha Vantage Overview
Alpha Vantage provides real-time and historical financial market data through a set of powerful and developer-friendly data APIs. From traditional asset classes (e.g., stocks, ETFs, mutual funds) to economic indicators, from foreign exchange rates to commodities, from fundamental data to technical indicators, Alpha Vantage is your one-stop-shop for enterprise-grade global market data delivered through cloud-based APIs.

### Features
- **Easy Integration:** Simple and intuitive functions to connect with the Alpha Vantage API, tailored for seamless use within the QuantConnect LEAN Algorithmic Trading Engine.

- **Rich Financial Data:** Access a wide range of financial data, including stock quotes, technical indicators, historical prices, and more, provided by Alpha Vantage.

- **Flexible Configuration:** The library supports various configuration options, including API key, allowing users to tailor their requests to specific needs.

- **Symbol SecurityType Support:** The library currently supports the following symbol security types:
  - [x] Equity
  - [ ] Option
  - [ ] Commodity
  - [ ] Forex
  - [ ] Future
  - [ ] Crypto
  - [ ] Index

- **Backtesting and Research:** Test your algorithm in backtest and research modes within [QuantConnect.Lean CLI](https://www.quantconnect.com/docs/v2/lean-cli), leveraging the Alpha Vantage API data to refine and optimize your trading strategies.

### Contribute to the Project
Contributions to this open-source project are welcome! If you find any issues, have suggestions for improvements, or want to add new features, please open an issue or submit a pull request.

### Installation

To contribute to the Alpha Vantage API Connector Library for .NET 6 within QuantConnect LEAN, follow these steps:
1. **Obtain API Key:** Sign up for a free Alpha Vantage API key [here](https://www.alphavantage.co/) if you don't have one.
2. **Fork the Project:** Fork the repository by clicking the "Fork" button at the top right of the GitHub page.
3. Clone Your Forked Repository:
```
git clone https://github.com/your-username/alpha-vantage-connector-dotnet.git
```
4. **Configuration:**
  - Set the `alpha-vantage-api-key` in your QuantConnect configuration (config.json or environment variables).
  - [optional] Set the `alpha-vantage-price-plan` (by default: Free)
```
{
    "alpha-vantage-api-key": "",
    "alpha-vantage-price-plan": "",
}
```

### Price Plan
For detailed information on Alpha Vantage's pricing plans, please refer to the [Alpha Vantage Pricing](https://www.alphavantage.co/premium/) page.

### Documentation
Refer to the [documentation](https://www.quantconnect.com/docs/v2/lean-cli/datasets/alphavantage) for detailed information on the library's functions, parameters, and usage examples.

### License
This project is licensed under the MIT License - see the [LICENSE](#) file for details.

Happy coding and algorithmic trading!
