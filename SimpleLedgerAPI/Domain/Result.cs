namespace SimpleLedgerAPI.Domain
{
    public record Result<T>(bool IsSuccess, T? Data = default, string? ErrorMessage = null)
    {
        public static Result<T> Success(T data) => new(true, data);
        public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
    }
}
