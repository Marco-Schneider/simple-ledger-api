namespace SimpleLedgerAPI.Domain
{
    public record EventRequest(string Type, string? Origin, string? Destination, decimal Amount);
}
