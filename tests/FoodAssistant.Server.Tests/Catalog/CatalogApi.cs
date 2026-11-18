using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodAssistant.Contracts.Catalog;
using FoodAssistant.Contracts.Inventory;
using FoodAssistant.Server.Tests.Auth;

namespace FoodAssistant.Server.Tests.Catalog;

/// <summary>Requests of the catalog endpoint tests, including the ones with <c>If-Match</c>.</summary>
internal static class CatalogApi
{
    public static string Products(Guid household) => $"/api/v1/households/{household}/products";

    public static string Locations(Guid household) => $"/api/v1/households/{household}/storage-locations";

    public static CreateProductRequest NewProduct(string name = "Milk", params string[] barcodes) =>
        new(null, name, null, "dairy", "l", null, null, null, barcodes, null);

    public static UpdateProductRequest Update(string name, params string[] barcodes) =>
        new(name, null, "dairy", "l", null, null, null, barcodes);

    public static StorageLocationRequest Location(string name = "Fridge", StorageTypeDto type = StorageTypeDto.Chilled) =>
        new(null, name, type, null);

    public static string ETag(long version) => $"\"{version}\"";

    public static Task<HttpResponseMessage> SendAsync<T>(TestClient client, HttpMethod method, string url, T? body, string? etag)
        where T : class
    {
        var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        return client.Http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> DeleteAsync(TestClient client, string url, string? etag) =>
        SendAsync<object>(client, HttpMethod.Delete, url, null, etag);

    public static async Task<ProductDto> CreateProductAsync(TestClient client, Guid household, CreateProductRequest request)
    {
        using var response = await client.PostAsync(Products(household), request);
        return await TestClient.ReadAsync<ProductDto>(response, HttpStatusCode.Created);
    }

    public static async Task<StorageLocationDto> CreateLocationAsync(TestClient client, Guid household, StorageLocationRequest request)
    {
        using var response = await client.PostAsync(Locations(household), request);
        return await TestClient.ReadAsync<StorageLocationDto>(response, HttpStatusCode.Created);
    }
}
