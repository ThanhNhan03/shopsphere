using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using ShopSphere.Basket.Infrastructure;
using ShopSphere.SharedKernel;
using Xunit;

namespace ShopSphere.Tests.Unit;

public sealed class InventoryAvailabilityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AvailableReturnsQuantityAndCallsExpectedEndpoint()
    {
        var productId = Guid.NewGuid();
        Uri? requestedUri = null;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { productId, availableQuantity = 7 })
        };
        var handler = HandlerReturning(response, request => requestedUri = request.RequestUri);
        var sut = new InventoryAvailability(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://inventory.test")
        });

        var quantity = await sut.Available(productId, CancellationToken.None);

        Assert.Equal(7, quantity);
        Assert.Equal($"https://inventory.test/api/inventory/{productId}", requestedUri?.ToString());
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Get),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MissingStockReturnsZero()
    {
        var handler = HandlerReturning(new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = ClientUsing(handler);

        var quantity = await sut.Available(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, quantity);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-20)]
    [Trait("Category", "Unit")]
    public async Task InvalidUpstreamQuantityIsRejected(int availableQuantity)
    {
        var productId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { productId, availableQuantity })
        };
        var sut = ClientUsing(HandlerReturning(response));

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            sut.Available(productId, CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NetworkFailureBecomesStableServiceUnavailableError()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("simulated network failure"));
        var sut = ClientUsing(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            sut.Available(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("Stock availability cannot be verified. Please try again shortly.", error.Message);
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private static InventoryAvailability ClientUsing(Mock<HttpMessageHandler> handler) =>
        new(new HttpClient(handler.Object) { BaseAddress = new Uri("https://inventory.test") });

    private static Mock<HttpMessageHandler> HandlerReturning(
        HttpResponseMessage response,
        Action<HttpRequestMessage>? callback = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .Callback((HttpRequestMessage request, CancellationToken _) => callback?.Invoke(request))
            .ReturnsAsync(response);
        return handler;
    }
}
