using System.Net;
using System.Text;
using Travether.Api.Places;

namespace Travether.Api.Tests;

public sealed class PlaceSearchTests
{
    private const string PhotonReply = """
        {"type":"FeatureCollection","features":[
          {"geometry":{"type":"Point","coordinates":[98.9933,18.7877]},
           "properties":{"name":"Tha Phae Gate","district":"Old City","city":"Chiang Mai","state":"Chiang Mai Province","country":"Thailand"}},
          {"geometry":{"type":"Point","coordinates":[98.95,18.80]},
           "properties":{"street":"Huay Kaew Road","housenumber":"21","city":"Chiang Mai","country":"Thailand"}},
          {"geometry":{"type":"Point","coordinates":[0,0]},"properties":{"country":"Nowhere"}}
        ]}
        """;

    private sealed class Canned : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PhotonReply, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task Photon_results_become_places_with_a_coarse_area()
    {
        using var handler = new Canned();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://photon.test/") };
        var search = new PhotonPlaceSearch(http);

        var places = await search.SearchAsync("tha phae", 18.79, 98.99, default);

        Assert.Equal(2, places.Count);
        Assert.Equal(new PlaceDto("Tha Phae Gate", "Old City, Chiang Mai", 18.7877, 98.9933), places[0]);
        Assert.Equal("Huay Kaew Road 21", places[1].Name);
        Assert.Equal("Chiang Mai, Thailand", places[1].Area);
        Assert.Equal("https://photon.test/api/?limit=6&q=tha%20phae&lat=18.79&lon=98.99", handler.LastUri!.AbsoluteUri);
    }
}
