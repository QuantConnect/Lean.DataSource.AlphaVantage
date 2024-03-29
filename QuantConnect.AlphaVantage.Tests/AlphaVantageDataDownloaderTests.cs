﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Util;
using QuantConnect.Tests;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.AlphaVantage.Tests
{
    [TestFixture]
    public class AlphaVantageDataDownloaderTests
    {
        private AlphaVantageDataDownloader _downloader;
        private MarketHoursDatabase _marketHoursDatabase;

        [SetUp]
        public void SetUp()
        {
            _downloader = new();
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        }

        [TearDown]
        public void TearDown()
        {
            if (_downloader != null)
            {
                _downloader.Dispose();
            }
        }

        /// <summary>
        /// Provides a collection of valid test case data for the Downloader method.
        /// Each TestCaseData represents a valid set of data to be used in testing.
        /// It is important to note that the test cases should not exceed 25 requests. (Because free api key gives only 25 request per day)
        /// </summary>
        /// <returns>
        /// An IEnumerable of TestCaseData, each representing a valid set of data for the Downloader method.
        /// </returns>
        public static IEnumerable<TestCaseData> DownloaderValidCaseData
        {
            get
            {
                TestGlobals.Initialize();
                var AAPL = new Symbol(SecurityIdentifier.GenerateEquity("AAPL", Market.USA, false), "AAPL");
                yield return new TestCaseData(AAPL, Resolution.Minute, new DateTime(2024, 1, 1, 5, 30, 0), new DateTime(2024, 2, 1, 20, 0, 0), TickType.Trade);
                yield return new TestCaseData(AAPL, Resolution.Minute, new DateTime(2024, 1, 8, 9, 30, 0), new DateTime(2024, 1, 12, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(AAPL, Resolution.Minute, new DateTime(2015, 2, 2, 9, 30, 0), new DateTime(2015, 3, 1, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(AAPL, Resolution.Hour, new DateTime(2023, 11, 8, 9, 30, 0), new DateTime(2024, 2, 2, 16, 0, 0), TickType.Trade);
                yield return new TestCaseData(AAPL, Resolution.Daily, new DateTime(2023, 1, 8, 9, 30, 0), new DateTime(2024, 2, 2, 16, 0, 0), TickType.Trade);
            }
        }

        [TestCaseSource(nameof(DownloaderValidCaseData))]
        public void DownloadDataWithDifferentValidParameters(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc, TickType tickType)
        {
            var downloadParameters = new DataDownloaderGetParameters(symbol, resolution, startUtc, endUtc, tickType);

            var baseData = _downloader.Get(downloadParameters).ToList();

            Assert.IsNotEmpty(baseData);
            Assert.IsTrue(baseData.First().Time >= ConvertUtcTimeToSymbolExchange(symbol, startUtc));
            Assert.IsTrue(baseData.Last().Time <= ConvertUtcTimeToSymbolExchange(symbol, endUtc));

            foreach (var data in baseData)
            {
                Assert.IsTrue(data.DataType == MarketDataType.TradeBar);
                var tradeBar = data as TradeBar;
                Assert.Greater(tradeBar.Open, 0m);
                Assert.Greater(tradeBar.High, 0m);
                Assert.Greater(tradeBar.Low, 0m);
                Assert.Greater(tradeBar.Close, 0m);
                Assert.IsTrue(tradeBar.Period.ToHigherResolutionEquivalent(true) == resolution);
            }
        }

        public static IEnumerable<TestCaseData> DownloaderInvalidCaseData
        {
            get
            {
                var startUtc = new DateTime(2024, 1, 1);
                var endUtc = new DateTime(2024, 2, 1);
                var AAPL = new Symbol(SecurityIdentifier.GenerateEquity("AAPL", Market.USA, false), "AAPL");

                yield return new TestCaseData(AAPL, Resolution.Minute, startUtc, endUtc, TickType.Quote)
                    .SetDescription($"Not supported {nameof(TickType.Quote)} -> empty result");
                yield return new TestCaseData(AAPL, Resolution.Minute, startUtc, endUtc, TickType.OpenInterest)
                    .SetDescription($"Not supported {nameof(TickType.OpenInterest)} -> empty result");
                yield return new TestCaseData(AAPL, Resolution.Tick, startUtc, endUtc, TickType.Trade)
                    .SetDescription($"Not supported {nameof(Resolution.Tick)} -> throw Exception");
                yield return new TestCaseData(AAPL, Resolution.Second, startUtc, endUtc, TickType.Trade)
                    .SetDescription($"Not supported {nameof(Resolution.Second)} -> throw Exception");
                yield return new TestCaseData(AAPL, Resolution.Minute, endUtc, startUtc, TickType.Trade)
                    .SetDescription("startDateTime > endDateTime -> empty result");
                yield return new TestCaseData(Symbol.Create("USDJPY", SecurityType.Forex, Market.Oanda), Resolution.Minute, startUtc, endUtc, TickType.Trade)
                    .SetDescription($"Not supported {nameof(SecurityType.Forex)} -> empty result");
                yield return new TestCaseData(Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Coinbase), Resolution.Minute, startUtc, endUtc, TickType.Trade)
                    .SetDescription($"Not supported {nameof(SecurityType.Crypto)} -> empty result");
            }
        }

        [TestCaseSource(nameof(DownloaderInvalidCaseData))]
        public void DownloadDataWithDifferentInvalidParameters(Symbol symbol, Resolution resolution, DateTime start, DateTime end, TickType tickType)
        {
            var downloadParameters = new DataDownloaderGetParameters(symbol, resolution, start, end, tickType);

            Assert.IsNull(_downloader.Get(downloadParameters)?.ToList());
        }

        [TestCase("GOOG", "2014/04/01", "2014/06/02", "GOOG", Description = "GOOCV before 2014/04/02, GOOG")]
        [TestCase("GOOGL", "2015/10/3", "2015/12/29", "GOOGL", Description = "October 2, 2015. [GOOG -> GOOGL]")]
        [TestCase("META", "2021/11/3", "2022/1/2", "META", Description = "October 28, 2021. [FB -> META]")]
        public void DownloadHistoricalDataBeforeSymbolWasRestructured(string ticker, DateTime startUtc, DateTime endUtc, string expectedTicker)
        {
            var symbol = Symbol.Create(ticker, SecurityType.Equity, Market.USA);
            //var symbol = new Symbol(SecurityIdentifier.GenerateEquity(ticker, Market.USA, false), ticker);
            var downloadParameters = new DataDownloaderGetParameters(symbol, Resolution.Daily, startUtc, endUtc, TickType.Trade);

            var baseData = _downloader.Get(downloadParameters).ToList();

            Assert.IsNotEmpty(baseData);
            Assert.IsTrue(baseData.First().Time >= ConvertUtcTimeToSymbolExchange(symbol, startUtc));
            Assert.IsTrue(baseData.Last().Time <= ConvertUtcTimeToSymbolExchange(symbol, endUtc));

            foreach (var data in baseData)
            {
                Assert.IsTrue(data.DataType == MarketDataType.TradeBar);
                var tradeBar = data as TradeBar;
                Assert.Greater(tradeBar.Open, 0m);
                Assert.Greater(tradeBar.High, 0m);
                Assert.Greater(tradeBar.Low, 0m);
                Assert.Greater(tradeBar.Close, 0m);
                Assert.IsTrue(tradeBar.Period.ToHigherResolutionEquivalent(true) == Resolution.Daily);
            }
        }

        private DateTime ConvertUtcTimeToSymbolExchange(Symbol symbol, DateTime dateTimeUtc)
        {
            var exchangeTimeZone = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType).TimeZone;
            return dateTimeUtc.ConvertFromUtc(exchangeTimeZone);
        }
    }
}
