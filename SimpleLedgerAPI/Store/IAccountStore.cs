using SimpleLedgerAPI.Domain;

namespace SimpleLedgerAPI.Store
{
    public interface IAccountStore
    {
        Account? GetAccount(string accountId);
        void SaveAccount(Account account);
        void Reset();
    }
}
