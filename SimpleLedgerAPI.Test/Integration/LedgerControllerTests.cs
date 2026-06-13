using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using SimpleLedgerAPI.Domain;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SimpleLedgerAPI.Tests.Integration
{
    public class LedgerControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;

        public LedgerControllerTests(WebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient();
        }

        [Fact(DisplayName = "/event | When and invalid event is requested, should return bad request")]
        public async Task ProcessEventShouldReturnBadRequestForInvalidEvent()
        {
            // Arrange
            var invalidRequest = new EventRequest("refund", "100", "200", 15m);

            // Act
            var response = await _httpClient.PostAsJsonAsync("event", invalidRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

    }
}
