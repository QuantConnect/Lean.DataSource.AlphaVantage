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

using NodaTime;
using CsvHelper;
using RestSharp;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Api;
using QuantConnect.Data;
using QuantConnect.Util;
using System.Globalization;
using QuantConnect.Logging;
using Newtonsoft.Json.Linq;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using QuantConnect.Configuration;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace QuantConnect.AlphaVantage
{
    /// <summary>
    /// Alpha Vantage data downloader
    /// </summary>
    public class AlphaVantageDataDownloader : IDataDownloader, IDisposable
    {
        private readonly MarketHoursDatabase _marketHoursDatabase;
        private readonly IRestClient _avClient;
        private readonly RateGate _rateGate;
        private bool _disposed;

        /// <summary>
        /// Represents a mapping of symbols to their corresponding time zones for exchange information.
        /// </summary>
        private readonly ConcurrentDictionary<Symbol, DateTimeZone> _symbolExchangeTimeZones = new ConcurrentDictionary<Symbol, DateTimeZone>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AlphaVantageDataDownloader"/>
        /// getting the AlphaVantage API key from the configuration
        /// </summary>
        public AlphaVantageDataDownloader() : this(Config.Get("alpha-vantage-api-key"))
        {
        }

        /// <summary>
        /// Construct AlphaVantageDataDownloader with default RestClient
        /// </summary>
        /// <param name="apiKey">API key</param>
        public AlphaVantageDataDownloader(string apiKey) : this(new RestClient(), apiKey)
        {
        }

        /// <summary>
        /// Dependency injection constructor
        /// </summary>
        /// <param name="restClient">The <see cref="RestClient"/> to use</param>
        /// <param name="apiKey">API key</param>
        public AlphaVantageDataDownloader(IRestClient restClient, string apiKey)
        {
            _avClient = restClient;
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            _avClient.BaseUrl = new Uri("https://www.alphavantage.co/");
            _avClient.Authenticator = new AlphaVantageAuthenticator(apiKey);

            _rateGate = new RateGate(5, TimeSpan.FromMinutes(1)); // Free API is limited to 5 requests/minute

            ValidateSubscription();
        }

        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="dataDownloaderGetParameters">model class for passing in parameters for historical data</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            var symbol = dataDownloaderGetParameters.Symbol;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            if (endUtc < startUtc)
            {
                Log.Error($"{nameof(AlphaVantageDataDownloader)}.{nameof(Get)}:InvalidDateRange. The history request start date must precede the end date, no history returned");
                return Enumerable.Empty<BaseData>();
            }

            if (tickType != TickType.Trade)
            {
                Log.Error($"{nameof(AlphaVantageDataDownloader)}.{nameof(Get)}: Not supported data type - {tickType}. " +
                    $"Currently available support only for historical of type - TradeBar");
                return Enumerable.Empty<BaseData>();
            }

            var request = new RestRequest("query", DataFormat.Json);
            request.AddParameter("symbol", symbol.Value);
            request.AddParameter("datatype", "csv");

            IEnumerable<TimeSeries> data = null;
            switch (resolution)
            {
                case Resolution.Minute:
                case Resolution.Hour:
                    data = GetIntradayData(request, startUtc, endUtc, resolution);
                    break;
                case Resolution.Daily:
                    data = GetDailyData(request, startUtc, endUtc, symbol);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), $"{resolution} resolution not supported by API.");
            }

            var period = resolution.ToTimeSpan();
            var startBySymbolExchange = ConvertTickTimeBySymbol(symbol, startUtc);
            var endBySymbolExchange = ConvertTickTimeBySymbol(symbol, endUtc);

            return data.Where(d => d.Time >= startBySymbolExchange && d.Time <= endBySymbolExchange).Select(d => new TradeBar(d.Time, symbol, d.Open, d.High, d.Low, d.Close, d.Volume, period));
        }

        /// <summary>
        /// Get data from daily API
        /// </summary>
        /// <param name="request">Base request</param>
        /// <param name="startUtc">Start time</param>
        /// <param name="endUtc">End time</param>
        /// <param name="symbol">Symbol to download</param>
        /// <returns></returns>
        private IEnumerable<TimeSeries> GetDailyData(RestRequest request, DateTime startUtc, DateTime endUtc, Symbol symbol)
        {
            request.AddParameter("function", "TIME_SERIES_DAILY");

            // The default output only includes 100 trading days of data. If we want need more, specify full output
            if (GetBusinessDays(startUtc, endUtc, symbol) > 100)
            {
                request.AddParameter("outputsize", "full");
            }

            return GetTimeSeries(request);
        }

        /// <summary>
        /// This API returns current and 20+ years of historical intraday OHLCV time series of the equity specified
        /// https://www.alphavantage.co/documentation/#intraday-extended
        /// </summary>
        /// <remarks>The exchange uses Eastern Time for the US market</remarks>
        /// <param name="request">Base request</param>
        /// <param name="startUtc">Start time</param>
        /// <param name="endUtc">End time</param>
        /// <param name="resolution">Data resolution to request</param>
        /// <returns></returns>
        private IEnumerable<TimeSeries> GetIntradayData(RestRequest request, DateTime startUtc, DateTime endUtc, Resolution resolution)
        {
            request.AddParameter("function", "TIME_SERIES_INTRADAY");
            request.AddParameter("adjusted", "false");
            request.AddParameter("outputsize", "full");
            switch (resolution)
            {
                case Resolution.Minute:
                    request.AddParameter("interval", "1min");
                    break;
                case Resolution.Hour:
                    request.AddParameter("interval", "60min");
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{resolution} resolution not supported by intraday API.");
            }

            var slices = GetSlices(startUtc, endUtc);
            foreach (var slice in slices)
            {
                request.AddOrUpdateParameter("month", slice);
                var data = GetTimeSeries(request);
                foreach (var record in data)
                {
                    yield return record;
                }
            }
        }

        /// <summary>
        /// Execute request and parse response.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns><see cref="TimeSeries"/> data</returns>
        private IEnumerable<TimeSeries> GetTimeSeries(RestRequest request)
        {
            if (_rateGate.IsRateLimited)
            {
                Log.Trace("Requests are limited to 5 per minute. Reduce the time between start and end times or simply wait, and this process will continue automatically.");
            }

            _rateGate.WaitToProceed();
            //var url = _avClient.BuildUri(request);
            Log.Trace("Downloading /{0}?{1}", request.Resource, string.Join("&", request.Parameters));
            var response = _avClient.Get(request);

            if (response.ContentType != "application/x-download")
            {
                throw new FormatException($"Unexpected content received from API.\n{response.Content}");
            }

            using (var reader = new StringReader(response.Content))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    return csv.GetRecords<TimeSeries>()
                              .OrderBy(t => t.Time)
                              .ToList(); // Execute query before readers are disposed.
                }
            }
        }

        /// <summary>
        /// Get slice names for date range.
        /// See https://www.alphavantage.co/documentation/#intraday-extended
        /// </summary>
        /// <param name="startUtc">Start date</param>
        /// <param name="endUtc">End date</param>
        /// <returns>Slice names</returns>
        private static IEnumerable<string> GetSlices(DateTime startUtc, DateTime endUtc)
        {
            do
            {
                yield return startUtc.ToString("yyyy-MM");
                startUtc = startUtc.AddMonths(1);
            } while (startUtc.Date <= endUtc.Date);
        }

        /// <summary>
        /// From https://stackoverflow.com/questions/1617049/calculate-the-number-of-business-days-between-two-dates
        /// </summary>
        private int GetBusinessDays(DateTime start, DateTime end, Symbol symbol)
        {
            var exchangeHours = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);

            var current = start.Date;
            var days = 0;
            while (current < end)
            {
                if (exchangeHours.IsDateOpen(current))
                {
                    days++;
                }
                current = current.AddDays(1);
            }

            return days;
        }

        /// <summary>
        /// Converts the provided tick timestamp, given in DateTime UTC Kind, to the exchange time zone associated with the specified Lean symbol.
        /// </summary>
        /// <param name="symbol">The Lean symbol for which the timestamp is associated.</param>
        /// <param name="dateTimeUtc">The DateTime in Utc format Kind</param>
        /// <returns>A DateTime object representing the converted timestamp in the exchange time zone.</returns>
        private DateTime ConvertTickTimeBySymbol(Symbol symbol, DateTime dateTimeUtc)
        {
            if (!_symbolExchangeTimeZones.TryGetValue(symbol, out var exchangeTimeZone))
            {
                // read the exchange time zone from market-hours-database
                exchangeTimeZone = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType).TimeZone;
                _symbolExchangeTimeZones.TryAdd(symbol, exchangeTimeZone);
            }

            return dateTimeUtc.ConvertFromUtc(exchangeTimeZone);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    _rateGate.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private class ModulesReadLicenseRead : Api.RestResponse
        {
            [JsonProperty(PropertyName = "license")]
            public string License;

            [JsonProperty(PropertyName = "organizationId")]
            public string OrganizationId;
        }

        /// <summary>
        /// Validate the user of this project has permission to be using it via our web API.
        /// </summary>
        private static void ValidateSubscription()
        {
            try
            {
                const int productId = 333;
                var userId = Config.GetInt("job-user-id");
                var token = Config.Get("api-access-token");
                var organizationId = Config.Get("job-organization-id", null);
                // Verify we can authenticate with this user and token
                var api = new ApiConnection(userId, token);
                if (!api.Connected)
                {
                    throw new ArgumentException("Invalid api user id or token, cannot authenticate subscription.");
                }
                // Compile the information we want to send when validating
                var information = new Dictionary<string, object>()
                {
                    {"productId", productId},
                    {"machineName", Environment.MachineName},
                    {"userName", Environment.UserName},
                    {"domainName", Environment.UserDomainName},
                    {"os", Environment.OSVersion}
                };
                // IP and Mac Address Information
                try
                {
                    var interfaceDictionary = new List<Dictionary<string, object>>();
                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up))
                    {
                        var interfaceInformation = new Dictionary<string, object>();
                        // Get UnicastAddresses
                        var addresses = nic.GetIPProperties().UnicastAddresses
                            .Select(uniAddress => uniAddress.Address)
                            .Where(address => !IPAddress.IsLoopback(address)).Select(x => x.ToString());
                        // If this interface has non-loopback addresses, we will include it
                        if (!addresses.IsNullOrEmpty())
                        {
                            interfaceInformation.Add("unicastAddresses", addresses);
                            // Get MAC address
                            interfaceInformation.Add("MAC", nic.GetPhysicalAddress().ToString());
                            // Add Interface name
                            interfaceInformation.Add("name", nic.Name);
                            // Add these to our dictionary
                            interfaceDictionary.Add(interfaceInformation);
                        }
                    }
                    information.Add("networkInterfaces", interfaceDictionary);
                }
                catch (Exception)
                {
                    // NOP, not necessary to crash if fails to extract and add this information
                }
                // Include our OrganizationId if specified
                if (!string.IsNullOrEmpty(organizationId))
                {
                    information.Add("organizationId", organizationId);
                }
                var request = new RestRequest("modules/license/read", Method.POST) { RequestFormat = DataFormat.Json };
                request.AddParameter("application/json", JsonConvert.SerializeObject(information), ParameterType.RequestBody);
                api.TryRequest(request, out ModulesReadLicenseRead result);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Request for subscriptions from web failed, Response Errors : {string.Join(',', result.Errors)}");
                }

                var encryptedData = result.License;
                // Decrypt the data we received
                DateTime? expirationDate = null;
                long? stamp = null;
                bool? isValid = null;
                if (encryptedData != null)
                {
                    // Fetch the org id from the response if it was not set, we need it to generate our validation key
                    if (string.IsNullOrEmpty(organizationId))
                    {
                        organizationId = result.OrganizationId;
                    }
                    // Create our combination key
                    var password = $"{token}-{organizationId}";
                    var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                    // Split the data
                    var info = encryptedData.Split("::");
                    var buffer = Convert.FromBase64String(info[0]);
                    var iv = Convert.FromBase64String(info[1]);
                    // Decrypt our information
                    using var aes = new AesManaged();
                    var decryptor = aes.CreateDecryptor(key, iv);
                    using var memoryStream = new MemoryStream(buffer);
                    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using var streamReader = new StreamReader(cryptoStream);
                    var decryptedData = streamReader.ReadToEnd();
                    if (!decryptedData.IsNullOrEmpty())
                    {
                        var jsonInfo = JsonConvert.DeserializeObject<JObject>(decryptedData);
                        expirationDate = jsonInfo["expiration"]?.Value<DateTime>();
                        isValid = jsonInfo["isValid"]?.Value<bool>();
                        stamp = jsonInfo["stamped"]?.Value<int>();
                    }
                }
                // Validate our conditions
                if (!expirationDate.HasValue || !isValid.HasValue || !stamp.HasValue)
                {
                    throw new InvalidOperationException("Failed to validate subscription.");
                }

                var nowUtc = DateTime.UtcNow;
                var timeSpan = nowUtc - Time.UnixTimeStampToDateTime(stamp.Value);
                if (timeSpan > TimeSpan.FromHours(12))
                {
                    throw new InvalidOperationException("Invalid API response.");
                }
                if (!isValid.Value)
                {
                    throw new ArgumentException($"Your subscription is not valid, please check your product subscriptions on our website.");
                }
                if (expirationDate < nowUtc)
                {
                    throw new ArgumentException($"Your subscription expired {expirationDate}, please renew in order to use this product.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"{nameof(AlphaVantageDataDownloader)}.{nameof(ValidateSubscription)}: Failed during validation, shutting down. Error : {e.Message}");
                throw;
            }
        }
    }
}
