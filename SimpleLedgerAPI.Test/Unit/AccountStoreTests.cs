using SimpleLedgerAPI.Domain;
using SimpleLedgerAPI.Store;
using Xunit;

namespace SimpleLedgerAPI.Tests.Unit
{
    public class AccountStoreTests
    {
        private readonly InMemoryAccountStore _accountStore;

        public AccountStoreTests()
        {
            _accountStore = new InMemoryAccountStore();
        }

        [Fact(DisplayName = "When an account does not exist, GetAccount should return null")]
        public void GetAccountShouldReturnNullWhenAccountDoesNotExist()
        {
            // Arrange
            _accountStore.Reset();

            // Act
            var result = _accountStore.GetAccount("1");

            // Assert
            Assert.Null(result);
        }

        [Fact(DisplayName = "When saving an account, the account should be correctly persisted in the store")]
        public void SaveAccountShouldPersistAccountCorrectly() 
        {
            // Arrange
            var account = new Account("1", 200m);

            // Act
            _accountStore.SaveAccount(account);
            var result = _accountStore.GetAccount("1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(account.Id, result.Id);
            Assert.Equal(account.Balance, result.Balance);
        }

        [Fact(DisplayName = "When resetting the in-memory store, all accounts should be cleared")]
        public void ResetShouldClearTheWholeStore()
        {
            // Arrange
            var account1 = new Account("1", 100m);
            var account2 = new Account("2", 200m);
            _accountStore.SaveAccount(account1);
            _accountStore.SaveAccount(account2);

            // Act
            _accountStore.Reset();

            // Assert
            Assert.Null(_accountStore.GetAccount("1"));
            Assert.Null(_accountStore.GetAccount("2"));
        }
    }
}
