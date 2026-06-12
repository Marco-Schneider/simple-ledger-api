using SimpleLedgerAPI.Domain;
using SimpleLedgerAPI.Services;
using SimpleLedgerAPI.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SimpleLedgerAPI.Tests.Unit
{
    public class AccountServiceTests
    {
        [Fact]
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
