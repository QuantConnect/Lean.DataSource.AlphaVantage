/*
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

using Moq;
using System;
using RestSharp;
using System.Net;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.AlphaVantage.Tests
{
    [TestFixture]
    public class AlphaVantageDataDownloaderMockTests
    {
        private const string API_KEY = "TESTKEY";
        private const string BASE_URL = "https://www.alphavantage.co/";

        private Mock<IRestClient> _avClient;
        private AlphaVantageDataDownloader _downloader;
        private readonly TradeBarComparer _tradeBarComparer = new TradeBarComparer();

        private Symbol AAPL;
        private Symbol IBM;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AAPL = new Symbol(SecurityIdentifier.GenerateEquity("AAPL", Market.USA, false), "AAPL");
            IBM = new Symbol(SecurityIdentifier.GenerateEquity("IBM", Market.USA, false), "IBM");
        }

        [SetUp]
        public void SetUp()
        {
            _avClient = new Mock<IRestClient>();
            _avClient.SetupAllProperties();

            _downloader = new AlphaVantageDataDownloader(_avClient.Object, API_KEY);
        }

        [TearDown]
        public void TearDown()
        {
            if (_downloader != null)
            {
                _downloader.Dispose();
            }
        }

        [Test]
        public void GetDailyLessThan100DaysGetsCompactDailyData()
        {
            var resolution = Resolution.Daily;
            var start = new DateTime(2021, 4, 4);
            var end = new DateTime(2021, 7, 12);

            var expectedBars = new[]
            {
                new TradeBar(DateTime.Parse("2021-04-05"), AAPL, 133.64m, 136.69m, 133.40m, 135.93m, 5471616),
                new TradeBar(DateTime.Parse("2021-04-06"), AAPL, 135.58m, 135.64m, 134.09m, 134.22m, 3620964),
            };

            IRestRequest request = null;
            _avClient.Setup(m => m.Execute(It.IsAny<IRestRequest>(), It.IsAny<Method>()))
                .Callback((IRestRequest r, Method m) => request = r)
                .Returns(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "application/x-download",
                    Content = "timestamp,open,high,low,close,volume\n" +
                              "2021-04-06,135.5800,135.6400,134.0900,134.2200,3620964\n" +
                              "2021-04-05,133.6400,136.6900,133.4000,135.9300,5471616\n"
                })
                .Verifiable();

            var result = _downloader.Get(new DataDownloaderGetParameters(AAPL, resolution, start, end));

            _avClient.Verify();
            var requestUrl = BuildUrl(request);
            Assert.AreEqual(Method.GET, request.Method);
            Assert.AreEqual($"{BASE_URL}query?symbol=AAPL&datatype=csv&function=TIME_SERIES_DAILY&outputsize=full", requestUrl);

            Assert.IsInstanceOf<IEnumerable<TradeBar>>(result);
            var bars = ((IEnumerable<TradeBar>)result).ToList();
            Assert.AreEqual(2, bars.Count);
            Assert.That(bars[0], Is.EqualTo(expectedBars[0]).Using(_tradeBarComparer));
            Assert.That(bars[1], Is.EqualTo(expectedBars[1]).Using(_tradeBarComparer));
        }

        [Test]
        public void GetDailyGreaterThan100DaysGetsFullDailyData()
        {
            var resolution = Resolution.Daily;
            var start = new DateTime(2021, 4, 4);
            var end = new DateTime(2024, 1, 1);

            var expectedBars = new[]
            {
                new TradeBar(DateTime.Parse("2021-04-05"), AAPL, 133.64m, 136.69m, 133.40m, 135.93m, 5471616),
                new TradeBar(DateTime.Parse("2021-04-06"), AAPL, 135.58m, 135.64m, 134.09m, 134.22m, 3620964),
            };

            IRestRequest request = null;
            _avClient.Setup(m => m.Execute(It.IsAny<IRestRequest>(), It.IsAny<Method>()))
                .Callback((IRestRequest r, Method m) => request = r)
                .Returns(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "application/x-download",
                    Content = "timestamp,open,high,low,close,volume\n" +
                              "2021-04-06,135.5800,135.6400,134.0900,134.2200,3620964\n" +
                              "2021-04-05,133.6400,136.6900,133.4000,135.9300,5471616\n"
                })
                .Verifiable();

            var result = _downloader.Get(new DataDownloaderGetParameters(AAPL, resolution, start, end));

            _avClient.Verify();
            var requestUrl = BuildUrl(request);
            Assert.AreEqual(Method.GET, request.Method);
            Assert.AreEqual($"{BASE_URL}query?symbol=AAPL&datatype=csv&function=TIME_SERIES_DAILY&outputsize=full", requestUrl);

            Assert.IsInstanceOf<IEnumerable<TradeBar>>(result);
            var bars = ((IEnumerable<TradeBar>)result).ToList();
            Assert.AreEqual(2, bars.Count);
            Assert.That(bars[0], Is.EqualTo(expectedBars[0]).Using(_tradeBarComparer));
            Assert.That(bars[1], Is.EqualTo(expectedBars[1]).Using(_tradeBarComparer));
        }

        [TestCase(Resolution.Minute, "1min")]
        [TestCase(Resolution.Hour, "60min")]
        public void GetMinuteHourGetsIntradayData(Resolution resolution, string interval)
        {
            var year = DateTime.UtcNow.Year - 1;
            var start = new DateTime(year, 04, 05, 0, 0, 0);
            var end = new DateTime(year, 05, 06, 15, 0, 0);

            var expectedBars = new[]
            {
                new TradeBar(start.AddHours(9.5), IBM, 133.71m, 133.72m, 133.62m, 133.62m, 1977),
                new TradeBar(start.AddHours(10.5), IBM, 134.30m, 134.56m, 134.245m, 134.34m, 154723),
                new TradeBar(end.AddHours(-5.5), IBM, 135.54m, 135.56m, 135.26m, 135.28m, 2315),
                new TradeBar(end.AddHours(-4.5), IBM, 134.905m,134.949m, 134.65m, 134.65m, 101997),
            };

            var responses = new[]
            {
                "time,open,high,low,close,volume\n" +
                $"{year}-04-05 10:30:00,134.3,134.56,134.245,134.34,154723\n" +
                $"{year}-04-05 09:30:00,133.71,133.72,133.62,133.62,1977\n",
                "time,open,high,low,close,volume\n" +
                $"{year}-05-06 10:30:00,134.905,134.949,134.65,134.65,101997\n" +
                $"{year}-05-06 09:30:00,135.54,135.56,135.26,135.28,2315\n",
            };

            var requestCount = 0;
            var requestUrls = new List<string>();
            _avClient.Setup(m => m.Execute(It.IsAny<IRestRequest>(), It.IsAny<Method>()))
                .Callback((IRestRequest r, Method m) => requestUrls.Add(BuildUrl(r)))
                .Returns(() => new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "application/x-download",
                    Content = responses[requestCount++]
                })
                .Verifiable();

            var result = _downloader.Get(new DataDownloaderGetParameters(IBM, resolution, start, end)).ToList();

            _avClient.Verify();
            Assert.AreEqual(2, requestUrls.Count);
            Assert.AreEqual($"{BASE_URL}query?symbol=IBM&datatype=csv&function=TIME_SERIES_INTRADAY&adjusted=false&outputsize=full&interval={interval}&month=2023-04", requestUrls[0]);
            Assert.AreEqual($"{BASE_URL}query?symbol=IBM&datatype=csv&function=TIME_SERIES_INTRADAY&adjusted=false&outputsize=full&interval={interval}&month=2023-05", requestUrls[1]);

            var bars = result.Cast<TradeBar>().ToList();
            Assert.AreEqual(4, bars.Count);
            Assert.That(bars[0], Is.EqualTo(expectedBars[0]).Using(_tradeBarComparer));
            Assert.That(bars[1], Is.EqualTo(expectedBars[1]).Using(_tradeBarComparer));
            Assert.That(bars[2], Is.EqualTo(expectedBars[2]).Using(_tradeBarComparer));
            Assert.That(bars[3], Is.EqualTo(expectedBars[3]).Using(_tradeBarComparer));
        }

        [TestCase(Resolution.Tick)]
        [TestCase(Resolution.Second)]
        public void GetUnsupportedResolutionThrowsException(Resolution resolution)
        {
            var start = DateTime.UtcNow.AddMonths(-2);
            var end = DateTime.UtcNow;

            Assert.IsNull(_downloader.Get(new DataDownloaderGetParameters(IBM, resolution, start, end))?.ToList());
        }

        [TestCase(Resolution.Minute)]
        [TestCase(Resolution.Hour)]
        [TestCase(Resolution.Daily)]
        public void UnexpectedResponseContentTypeThrowsException(Resolution resolution)
        {
            var start = DateTime.UtcNow.AddMonths(-2);
            var end = DateTime.UtcNow;

            _avClient.Setup(m => m.Execute(It.IsAny<IRestRequest>(), It.IsAny<Method>()))
                .Returns(() => new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "application/json"
                })
                .Verifiable();

            Assert.Throws<FormatException>(() => _downloader.Get(new DataDownloaderGetParameters(IBM, resolution, start, end)).ToList());
        }

        [Test]
        public void GetIntradayDataGreaterThanTwoYears()
        {
            var resolution = Resolution.Minute;
            var start = new DateTime(2020, 4, 4);
            var end = new DateTime(2020, 6, 5);

            var expectedBars = new[]
            {
                new TradeBar(DateTime.Parse("2020-04-05 7:34:00"), IBM, 133.64m, 136.69m, 133.40m, 135.93m, 5471616),
                new TradeBar(DateTime.Parse("2020-04-05 7:35:00"), IBM, 135.58m, 135.64m, 134.09m, 134.22m, 3620964),
            };

            IRestRequest request = null;
            _avClient.Setup(m => m.Execute(It.IsAny<IRestRequest>(), It.IsAny<Method>()))
                .Callback((IRestRequest r, Method m) => request = r)
                .Returns(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ContentType = "application/x-download",
                    Content = "timestamp,open,high,low,close,volume\n" +
                              "2020-04-05 7:34:00,133.6400,136.6900,133.4000,135.9300,5471616\n" +
                              "2020-04-05 7:35:00,135.5800,135.6400,134.0900,134.2200,3620964\n" +
                              "2020-04-05 7:35:00,135.5800,135.6400,134.0900,134.2200,3620964\n" +
                              "2020-04-06 7:36:00,133.6400,136.6900,133.4000,135.9300,5471616\n" +
                              "2020-05-06 7:37:00,133.6400,136.6900,133.4000,135.9300,5471616\n" +
                              "2020-05-07 7:38:00,133.6400,136.6900,133.4000,135.9300,5471616\n" +
                              "2020-05-07 7:39:00,133.6400,136.6900,133.4000,135.9300,5471616\n"
                })
                .Verifiable();

            var result = _downloader.Get(new DataDownloaderGetParameters(IBM, resolution, start, end)).ToList();

            _avClient.Verify();
            var requestUrl = BuildUrl(request);
            Assert.That(request.Method, Is.EqualTo(Method.GET));
            Assert.That(requestUrl, Is.EqualTo($"{BASE_URL}query?symbol=IBM&datatype=csv&function=TIME_SERIES_INTRADAY&adjusted=false&outputsize=full&interval=1min&month=2020-06"));

            Assert.That(result.Count, Is.EqualTo(7 * 3)); // 6 mock result * 3 request per each month
            Assert.That(result[0], Is.EqualTo(expectedBars[0]).Using(_tradeBarComparer));
            Assert.That(result[1], Is.EqualTo(expectedBars[1]).Using(_tradeBarComparer));
        }

        [Test]
        public void AuthenticatorAddsApiKeyToRequest()
        {
            var request = new Mock<IRestRequest>();
            var authenticator = _avClient.Object.Authenticator;

            Assert.NotNull(authenticator);
            authenticator.Authenticate(_avClient.Object, request.Object);
            request.Verify(m => m.AddOrUpdateParameter("apikey", API_KEY));
        }

        private string BuildUrl(IRestRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var client = new RestClient(BASE_URL);
            var uri = client.BuildUri(request);

            return uri.ToString();
        }

        private class TradeBarComparer : IEqualityComparer<TradeBar>
        {
            public bool Equals(TradeBar x, TradeBar y)
            {
                if (x == null || y == null)
                    return false;

                return x.Symbol == y.Symbol &&
                       x.Time == y.Time &&
                       x.Open == y.Open &&
                       x.High == y.High &&
                       x.Low == y.Low &&
                       x.Close == y.Close &&
                       x.Volume == y.Volume;
            }

            public int GetHashCode(TradeBar obj) => obj.GetHashCode();
        }
    }
}
