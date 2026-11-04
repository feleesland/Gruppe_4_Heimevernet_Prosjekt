using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text.Json;

namespace Heimevernet_Prosjekt.Services;

// One place or address suggestion from Kartverket
public sealed record PlaceSuggestion(string Name, string Description, double Latitude, double Longitude);

// Searches Kartverket's place name and address APIs for all of Norway (free, official Norwegian data) (help with AI)
// Docs: Make HTTP requests using IHttpClientFactory in ASP.NET Core (typed clients)
// https://learn.microsoft.com/aspnet/core/fundamentals/http-requests
public sealed class KartverketService(HttpClient httpClient, ILogger<KartverketService> logger)
{
    private static readonly TextInfo Norwegian = new CultureInfo("nb-NO").TextInfo;

    // Up to 3 place names (towns, districts ...) and addresses, max 5 in total.
    // nearLat/nearLng (later: "Min posisjon") sorts the closest first; without them the whole country is treated the same
    public async Task<List<PlaceSuggestion>> SearchAsync(string text, double? nearLat = null, double? nearLng = null, int max = 5)
    {
        // A number (e.g. "Markens gate 12") means one exact address; without a number we show one suggestion per street per town
        var hasNumber = text.Any(char.IsDigit);

        // First "starts with" (exact spelling) in both APIs, at the same time so it's faster
        var placesTask = GetPlaceNamesAsync(text + "*", fuzzy: false);
        var addressesTask = GetAddressesAsync(text + "*", fuzzy: false, onePerStreet: !hasNumber);
        await Task.WhenAll(placesTask, addressesTask);
        var places = placesTask.Result;
        var addresses = addressesTask.Result;

        // Only if nothing at all was found: similar spellings (fuzzy), for typos
        if (places.Count == 0 && addresses.Count == 0)
        {
            placesTask = GetPlaceNamesAsync(text, fuzzy: true);
            addressesTask = GetAddressesAsync(text, fuzzy: true, onePerStreet: !hasNumber);
            await Task.WhenAll(placesTask, addressesTask);
            places = placesTask.Result;
            addresses = addressesTask.Result;
        }

        var rankedPlaces = Rank(places, text, nearLat, nearLng).Take(3);
        var rankedAddresses = Rank(addresses, text, nearLat, nearLng);

        // With a number the user is looking for an address: show addresses first
        var result = hasNumber
            ? rankedAddresses.Concat(rankedPlaces)
            : rankedPlaces.Concat(rankedAddresses);
        return result.Take(max).ToList();
    }

    // Kartverket Stedsnavn API, e.g. "Kvadraturen"
    private async Task<List<PlaceSuggestion>> GetPlaceNamesAsync(string search, bool fuzzy)
    {
        var url = $"https://api.kartverket.no/stedsnavn/v1/navn?sok={Uri.EscapeDataString(search)}&fuzzy={(fuzzy ? "true" : "false")}&utkoordsys=4258&treffPerSide=20";
        var result = new List<PlaceSuggestion>();
        try
        {
            using var json = JsonDocument.Parse(await httpClient.GetStringAsync(url));
            foreach (var n in json.RootElement.GetProperty("navn").EnumerateArray())
            {
                var type = n.TryGetProperty("navneobjekttype", out var t) ? t.GetString() : null;

                // Streets come from the address API instead (exact coordinates, and not shown twice)
                if (type == "Adressenavn")
                {
                    continue;
                }

                var point = n.GetProperty("representasjonspunkt");
                var municipality = n.TryGetProperty("kommuner", out var k) && k.GetArrayLength() > 0
                    ? k[0].GetProperty("kommunenavn").GetString()
                    : null;

                result.Add(new PlaceSuggestion(
                    n.GetProperty("skrivemåte").GetString() ?? "",
                    string.Join(", ", new[] { type, municipality }.Where(s => !string.IsNullOrEmpty(s))),
                    point.GetProperty("nord").GetDouble(),
                    point.GetProperty("øst").GetDouble()));
            }
        }
        catch (Exception ex)
        {
            // Kartverket down or the answer looked different: the search still works, just without places
            logger.LogWarning(ex, "Kartverket place name search failed for {Text}", search);
        }
        return result;
    }

    // Kartverket Adresse API, e.g. "Markens gate 12" or "markens gate kristiansand"
    // onePerStreet: "Markens gate · Kristiansand" once, instead of every house number
    private async Task<List<PlaceSuggestion>> GetAddressesAsync(string search, bool fuzzy, bool onePerStreet)
    {
        var hits = onePerStreet ? 100 : 20; // more hits when grouping, so several towns can show up
        var url = $"https://ws.geonorge.no/adresser/v1/sok?sok={Uri.EscapeDataString(search)}&fuzzy={(fuzzy ? "true" : "false")}&utkoordsys=4258&treffPerSide={hits}";
        var result = new List<PlaceSuggestion>();
        var seenStreets = new HashSet<string>();
        try
        {
            using var json = JsonDocument.Parse(await httpClient.GetStringAsync(url));
            foreach (var a in json.RootElement.GetProperty("adresser").EnumerateArray())
            {
                var point = a.GetProperty("representasjonspunkt");
                var lat = point.GetProperty("lat").GetDouble();
                var lon = point.GetProperty("lon").GetDouble();

                if (onePerStreet)
                {
                    var street = a.TryGetProperty("adressenavn", out var sn) ? sn.GetString() ?? "" : "";
                    var municipality = Nice(a.TryGetProperty("kommunenavn", out var kn) ? kn.GetString() : null);

                    // Only the first address of each street in each municipality
                    if (street == "" || !seenStreets.Add(street + "|" + municipality))
                    {
                        continue;
                    }
                    result.Add(new PlaceSuggestion(street, municipality, lat, lon));
                }
                else
                {
                    var postnummer = a.TryGetProperty("postnummer", out var p) ? p.GetString() : null;
                    var poststed = Nice(a.TryGetProperty("poststed", out var s) ? s.GetString() : null);

                    result.Add(new PlaceSuggestion(
                        a.GetProperty("adressetekst").GetString() ?? "",
                        $"{postnummer} {poststed}".Trim(),
                        lat,
                        lon));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kartverket address search failed for {Text}", search);
        }
        return result;
    }

    // "KRISTIANSAND S" -> "Kristiansand S"
    private static string Nice(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "" : Norwegian.ToTitleCase(text.ToLowerInvariant());

    // Best matches first: exact name, then "starts with", then closest to nearLat/nearLng if we know it (help with AI)
    private static IEnumerable<PlaceSuggestion> Rank(List<PlaceSuggestion> list, string text, double? nearLat, double? nearLng)
    {
        var ordered = list
            .OrderByDescending(p => p.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase));

        if (nearLat != null && nearLng != null)
        {
            // Rough distance, only used for sorting (degrees, with longitude adjusted for latitude)
            var lngFactor = Math.Cos(nearLat.Value * Math.PI / 180);
            ordered = ordered.ThenBy(p => Math.Pow(p.Latitude - nearLat.Value, 2) + Math.Pow((p.Longitude - nearLng.Value) * lngFactor, 2));
        }

        return ordered.DistinctBy(p => (p.Name, p.Description));
    }

    // How the Kartverket client connects: IPv6 first, and IPv4 if IPv6 hasn't answered within 1 second.
    // On networks with broken IPv6, .NET would otherwise wait until the timeout, while the browser switches by itself (help with AI)
    // Docs: SocketsHttpHandler.ConnectCallback
    // https://learn.microsoft.com/dotnet/api/system.net.http.socketshttphandler.connectcallback
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var port = context.DnsEndPoint.Port;

        // 1) IPv6, max 1 second
        var ipv6 = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
        if (ipv6.Length > 0)
        {
            using var ipv6Timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ipv6Timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(ipv6, port, ipv6Timeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                socket.Dispose(); // IPv6 didn't work: try IPv4 below
            }
        }

        // 2) IPv4
        var ipv4 = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
        var ipv4Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await ipv4Socket.ConnectAsync(ipv4, port, cancellationToken);
            return new NetworkStream(ipv4Socket, ownsSocket: true);
        }
        catch
        {
            ipv4Socket.Dispose();
            throw;
        }
    }
}