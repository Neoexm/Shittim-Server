using BlueArchiveAPI.Handlers;
using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlueArchiveAPI.Tests
{
    public class AccountHandlerTests
    {
        private BAContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BAContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new BAContext(options);
        }

        [Fact]
        public async Task Login_NewUser_CreatesUserAndReturnsResponse()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var handler = new Account.Login(context);
            var request = new AccountCheckNexonRequest
            {
                NpSN = 12345678,
                NpToken = "test_token"
            };

            // Act
            var response = await handler.Handle(request);

            // Assert
            Assert.Equal(1, response.ResultState);
            Assert.True(response.AccountId > 0);
            Assert.NotNull(response.SessionKey);
            Assert.Equal(response.AccountId, response.SessionKey.AccountServerId);

            // Verify user was created in database
            var user = await context.Users.FirstOrDefaultAsync(u => u.PublisherAccountId == "12345678");
            Assert.NotNull(user);
            Assert.Equal("Sensei", user.Nickname);
            Assert.True(user.IsNew);
        }

        [Fact]
        public async Task Login_ExistingUser_ReturnsExistingUserData()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            
            // Create existing user
            var existingUser = new User
            {
                PublisherAccountId = "87654321",
                Nickname = "ExistingUser",
                IsNew = false
            };
            context.Users.Add(existingUser);
            await context.SaveChangesAsync();

            var handler = new Account.Login(context);
            var request = new AccountCheckNexonRequest
            {
                NpSN = 87654321,
                NpToken = "test_token"
            };

            // Act
            var response = await handler.Handle(request);

            // Assert
            Assert.Equal(1, response.ResultState);
            Assert.Equal(existingUser.Id, response.AccountId);
            Assert.NotNull(response.SessionKey);
            Assert.Equal(existingUser.Id, response.SessionKey.AccountServerId);

            // Verify no duplicate user was created
            var userCount = await context.Users.CountAsync(u => u.PublisherAccountId == "87654321");
            Assert.Equal(1, userCount);
        }

        [Fact]
        public async Task Auth_ReturnsHardcodedAccountData()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var handler = new Account.Auth();
            var request = new AccountAuthRequest
            {
                SessionKey = new SessionKey { AccountServerId = 123 }
            };

            // Act
            var response = await handler.Handle(request);

            // Assert
            Assert.NotNull(response.AccountDB);
            Assert.Equal(123, response.AccountDB.ServerId);
            Assert.Equal("佑树", response.AccountDB.Nickname);
            Assert.Equal(AccountState.Normal, response.AccountDB.State);
            Assert.Equal(100, response.AccountDB.Level);
            Assert.NotNull(response.AttendanceBookRewards);
            Assert.NotNull(response.MissionProgressDBs);
        }

        [Fact]
        public async Task GetTutorial_ReturnsDefaultTutorialIds()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var handler = new Account.GetTutorial();
            var request = new AccountGetTutorialRequest();

            // Act
            var response = await handler.Handle(request);

            // Assert
            Assert.NotNull(response.TutorialIds);
            Assert.Equal(5, response.TutorialIds.Count);
            Assert.Contains(1L, response.TutorialIds);
            Assert.Contains(5L, response.TutorialIds);
        }

        [Fact]
        public async Task SetTutorial_ReturnsEmptyResponse()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var handler = new Account.SetTutorial();
            var request = new AccountSetTutorialRequest
            {
                TutorialIds = new List<long> { 1, 2, 3 }
            };

            // Act
            var response = await handler.Handle(request);

            // Assert
            Assert.NotNull(response);
            // The response should be empty but not null
        }
    }
}