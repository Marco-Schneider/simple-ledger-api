using Microsoft.AspNetCore.Mvc.Testing;
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

        [Fact(DisplayName = "When the test suite is run, then all tests are marked with a green check")]
        public async Task TheFullTestSuitShouldRunThroughTheAPIAndGetMatchingResult()
        {

            // 1. Reset the state
            var resetResponse = await _httpClient.PostAsync("/reset", null);
            Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
            Assert.Equal("OK",(await resetResponse.Content.ReadAsStringAsync()));

            // 2. Balance of a non-existing account
            var balance404Response = await _httpClient.GetAsync("/balance?account_id=1234");
            Assert.Equal(HttpStatusCode.NotFound, balance404Response.StatusCode);

            // The test suite explicitly checks that the body of the 404 is exactly "0"
            Assert.Equal("0", (await balance404Response.Content.ReadAsStringAsync()));

            // 3. Create account with deposit operation
            var depositRequest = new EventRequest("deposit", null, "100", 10m);
            var depositResponse = await _httpClient.PostAsJsonAsync("/event", depositRequest);

            Assert.Equal(HttpStatusCode.Created, depositResponse.StatusCode);
            var depositResult = await depositResponse.Content.ReadFromJsonAsync<EventResponse>();

            // Validating the exact JSON structure and values
            Assert.NotNull(depositResult);
            Assert.NotNull(depositResult.Destination);
            Assert.Equal("100", depositResult.Destination.Id);

            Assert.Equal(10m, depositResult.Destination.Balance);
            Assert.Null(depositResult.Origin);


            // 4. Withdraw from existing account
            var withdrawRequest = new EventRequest("withdraw", "100", null, 5m);
            var withdrawResponse = await _httpClient.PostAsJsonAsync("/event", withdrawRequest);

            Assert.Equal(HttpStatusCode.Created, withdrawResponse.StatusCode);
            var withdrawResult = await withdrawResponse.Content.ReadFromJsonAsync<EventResponse>();

            Assert.NotNull(withdrawResult!.Origin);
            Assert.Equal("100", withdrawResult.Origin.Id);
            Assert.Equal(5m, withdrawResult.Origin.Balance);

            // 5. Transfer to new account
            var transferRequest = new EventRequest("transfer", "100", "300", 5m);
            var transferResponse = await _httpClient.PostAsJsonAsync("/event", transferRequest);

            Assert.Equal(HttpStatusCode.Created, transferResponse.StatusCode);
            var transferResult = await transferResponse.Content.ReadFromJsonAsync<EventResponse>();

            Assert.Equal("100", transferResult!.Origin!.Id);
            Assert.Equal(0m, transferResult.Origin.Balance);
            Assert.Equal("300", transferResult.Destination!.Id);
            Assert.Equal(5m, transferResult.Destination.Balance);
        }
    }
}
