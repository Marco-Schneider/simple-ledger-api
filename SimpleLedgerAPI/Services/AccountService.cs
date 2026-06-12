using SimpleLedgerAPI.Domain;
using SimpleLedgerAPI.Store;
using System.Collections.Concurrent;

namespace SimpleLedgerAPI.Services
{
    public class AccountService : IAccountService
    {
        private readonly InMemoryAccountStore _accountStore;
        private readonly ConcurrentDictionary<string, object> _accountLocks = new();

        public AccountService(InMemoryAccountStore accountStore)
        {
            _accountStore = accountStore;
        }

        private object GetAccountLock(string accountId)
        {
            return _accountLocks.GetOrAdd(accountId, _ => new object());
        }

        public Result<Account> GetBalance(string accountId)
        {
            lock (GetAccountLock(accountId))
            {
                var account = _accountStore.GetAccount(accountId);

                if (account == null)
                    return Result<Account>.Failure("Cannot retrieve balance. Account does not exist.");

                return Result<Account>.Success(account);
            }
        }

        public Result<Account> Deposit(string accountId, decimal amount)
        {
            lock (GetAccountLock(accountId))
            {
                var account = _accountStore.GetAccount(accountId) ?? new Account(accountId, 0);

                var updatedAccount = account with { Balance = account.Balance + amount };
                _accountStore.SaveAccount(updatedAccount);

                return Result<Account>.Success(updatedAccount);
            }
        }

        public Result<Account> Withdraw(string accountId, decimal amount)
        {
            lock (GetAccountLock(accountId))
            {
                var account = _accountStore.GetAccount(accountId);

                if (account == null)
                    return Result<Account>.Failure("Cannot process withdrawal. Account does not exist.");
                if (account.Balance < amount)
                    return Result<Account>.Failure("Cannot process withdrawal. Insufficient funds.");

                var updatedAccount = account with { Balance = account.Balance - amount };
                _accountStore.SaveAccount(updatedAccount);

                return Result<Account>.Success(updatedAccount);
            }
        }

        public Result<(Account Origin, Account Destination)> Transfer(string originId, string destinationId, decimal amount)
        {
            if (originId == destinationId)
                return Result<(Account, Account)>.Failure("Cannot transfer funds to the same account. Proceed with a deposit operation.");

            bool isOriginFirst = string.CompareOrdinal(originId, destinationId) < 0;
            string firstLockId = isOriginFirst ? originId : destinationId;
            string secondLockId = isOriginFirst ? destinationId : originId;

            lock (GetAccountLock(firstLockId))
            {
                lock (GetAccountLock(secondLockId))
                {
                    var origin = _accountStore.GetAccount(originId);
                    if (origin == null)
                        return Result<(Account, Account)>.Failure("Cannot process transference. Origin account does not exist.");
                    if (origin.Balance < amount)
                        return Result<(Account, Account)>.Failure("Cannot process transference. Account has insufficient funds.");

                    var destination = _accountStore.GetAccount(destinationId) ?? new Account(destinationId, 0);

                    var updatedOrigin = origin with { Balance = origin.Balance - amount };
                    var updatedDestination = destination with { Balance = destination.Balance + amount };

                    _accountStore.SaveAccount(updatedOrigin);
                    _accountStore.SaveAccount(updatedDestination);

                    return Result<(Account Origin, Account Destination)>.Success((updatedOrigin, updatedDestination));
                }
            }
        }

        public void Reset()
        {
            _accountStore.Reset();
        }
    }
}
