namespace SimpleLedgerAPI.Domain
{
    public record EventResponse(Account? Origin, Account? Destination);
}
