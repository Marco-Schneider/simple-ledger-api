using SimpleLedgerAPI.Domain;
using System.Collections.Concurrent;

namespace SimpleLedgerAPI.Store
{
    public class InMemoryAccountStore : IAccountStore
    {
        private readonly ConcurrentDictionary<string, Account> _accounts = new();

        public Account? GetAccount(string accountId)
        {
            _accounts.TryGetValue(accountId, out var account);
            return account;
        }

        public void SaveAccount(Account account)
        {
            _accounts.AddOrUpdate(account.Id, account, (key, existingAccount) => account);
        }

        public void Reset()
        {
            _accounts.Clear();
        }
    }
}
