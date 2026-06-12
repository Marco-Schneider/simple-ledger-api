using SimpleLedgerAPI.Domain;

namespace SimpleLedgerAPI.Services
{
    public interface IAccountService
    {
        Result<Account> GetBalance(string accountId);
        Result<Account> Deposit(string accountId, decimal amount);
        Result<Account> Withdraw(string accountId, decimal amount);
        Result<(Account Origin, Account Destination)> Transfer(string originId, string destinationId, decimal amount);
        void Reset(); //Leaving the reset here for now
    }
}
