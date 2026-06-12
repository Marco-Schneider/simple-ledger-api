using SimpleLedgerAPI.Domain;
using SimpleLedgerAPI.Services;
using SimpleLedgerAPI.Store;
using Xunit;

namespace SimpleLedgerAPI.Tests.Unit
{
    public class AccountServiceTests
    {
        private readonly InMemoryAccountStore _accountStore;
        private readonly AccountService _accountService;

        public AccountServiceTests()
        {
            _accountStore = new InMemoryAccountStore();
            _accountService = new AccountService(_accountStore);
        }

        [Fact(DisplayName = "When depositing to an account, if the account does not exist, then a new account should be created and its balance set")]
        public void DepositWhenAccountDoesNotExistShouldCreateAccountAndSetBalance()
        {
            // Arrange
            _accountStore.Reset();

            // Act
            var account = _accountService.Deposit("100", 50m);

            // Assert
            Assert.True(account.IsSuccess);
            Assert.Equal("100", account.Data!.Id);
            Assert.Equal(50m, account.Data!.Balance);

            Assert.Equal(50m, _accountStore.GetAccount("100")!.Balance);
        }

        [Fact(DisplayName = "When depositing to an account, if the account exists, then the amount should be added to existing balance")]
        public void DepositWhenAccountExistsShouldAddAmountToExistingBalance()
        {
            // Arrange
            _accountStore.Reset();
            _accountStore.SaveAccount(new Account("1", 20m));

            // Act
            var result = _accountService.Deposit("1", 50m);

            Assert.True(result.IsSuccess);
            Assert.Equal(70m, result.Data!.Balance);
        }

        [Fact(DisplayName = "When withdrawing from an account, if the account does not exist, then the operation should fail")]
        public void WithdrawWhenAccountDoesNotExistShouldFail()
        {
            // Arrange
            _accountStore.Reset();

            // Act
            var result = _accountService.Withdraw("1", 200m);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Theory(DisplayName = "Withdraw | Multiple scenarios |Shouldn't allow withdrawing with insufficient funds")]
        [InlineData(100, 50, true, 50)]
        [InlineData(100, 100, true, 0)] 
        [InlineData(100, 150, false, 100)]
        public void WithdrawingShouldOperateWithinBusinessRules(decimal initialBalance,decimal withdrawAmount,
            bool expectedSuccess,decimal expectedFinalBalance)
        {
            // Arrange
            _accountStore.Reset();
            _accountStore.SaveAccount(new Account("100", initialBalance));

            // Act
            var result = _accountService.Withdraw("100", withdrawAmount);

            // Assert
            Assert.Equal(expectedSuccess, result.IsSuccess);
            Assert.Equal(expectedFinalBalance, _accountStore.GetAccount("100")!.Balance);
        }

        [Fact(DisplayName = "When transfering in between accounts, tranfers should prevent deadlocking | Ensuring our locking strategy works")]
        public async Task TransferShouldHaveAtomicityMaintainingBalanceBetweenAccounts()
        {
            // Arrange
            var store = new InMemoryAccountStore();
            var service = new AccountService(store);

            store.SaveAccount(new Account("1", 1000m));
            store.SaveAccount(new Account("2", 1000m));

            var tasks = new Task[100];

            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                    tasks[i] = Task.Run(() => service.Transfer("1", "2", 10m));
                else
                    tasks[i] = Task.Run(() => service.Transfer("2", "1", 10m));
            }

            await Task.WhenAll(tasks);

            var finalAccountA = store.GetAccount("1");
            var finalAccountB = store.GetAccount("2");

            Assert.Equal(1000m, finalAccountA!.Balance);
            Assert.Equal(1000m, finalAccountB!.Balance);

            Assert.Equal(2000m, (finalAccountA.Balance + finalAccountB.Balance));
        }
    }
}
