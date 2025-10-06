using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace BlueArchiveAPI.Handlers
{
    public static class Account
    {
        public class Login : BaseHandler<AccountCheckNexonRequest, AccountCheckNexonResponse>
        {
            private readonly BAContext _dbContext;

            public Login()
            {
                // Create our own context for the parameterless constructor
                var options = new DbContextOptionsBuilder<BAContext>()
                    .UseSqlite("Data Source=BlueArchive.db")
                    .Options;
                _dbContext = new BAContext(options);
            }

            public Login(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountCheckNexonResponse> Handle(AccountCheckNexonRequest request)
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PublisherAccountId == request.NpSN.ToString());

                if (user == null)
                {
                    // User not found, so we're creating a new one. How thrilling.
                    user = new Models.User
                    {
                        Nickname = "Sensei", // You can change this later. Or not. I don't care.
                        PublisherAccountId = request.NpSN.ToString(),
                        // Add any other default fields you deem necessary for a new user.
                    };
                    _dbContext.Users.Add(user);
                    await _dbContext.SaveChangesAsync();
                }

                var session = new SessionKey
                {
                    AccountServerId = user.Id,
                    MxToken = "some_generated_token" // You should probably generate a real token here.
                };

                return new AccountCheckNexonResponse
                {
                    ResultState = 1,
                    AccountId = user.Id,
                    SessionKey = session
                };
            }
        }

        public class LoginSync : BaseHandler<AccountLoginSyncRequest, AccountLoginSyncResponse>
        {
            protected override async Task<AccountLoginSyncResponse> Handle(AccountLoginSyncRequest request)
            {
                // This is still mostly garbage, but at least it's not reading from a file.
                // You'll need to fetch real data from your database here.
                var result = new AccountLoginSyncResponse
                {
                    AccountCurrencySyncResponse = new AccountCurrencySyncResponse
                    {
                        AccountCurrencyDB = new AccountCurrencyDB
                        {
                            CurrencyDict = new System.Collections.Generic.Dictionary<CurrencyTypes, long>
                            {
                                { CurrencyTypes.Gem, 99999999 },
                                { CurrencyTypes.GemBonus, 99999999 }
                            }
                        }
                    },
                    // You need to fill out the rest of this response with actual data.
                    // Don't just leave it empty like your last attempt.
                };

                return result;
            }
        }

        public class GetTutorial : BaseHandler<AccountGetTutorialRequest, AccountGetTutorialResponse>
        {
            protected override async Task<AccountGetTutorialResponse> Handle(AccountGetTutorialRequest request)
            {
                return new AccountGetTutorialResponse
                {
                    TutorialIds = new System.Collections.Generic.List<long> { 1, 2, 3, 4, 5 }
                };
            }
        }

        public class SetTutorial : BaseHandler<AccountSetTutorialRequest, AccountSetTutorialResponse>
        {
            protected override async Task<AccountSetTutorialResponse> Handle(AccountSetTutorialRequest request)
            {
                return new AccountSetTutorialResponse();
            }
        }

        public class Auth : BaseHandler<AccountAuthRequest, AccountAuthResponse>
        {
            protected override async Task<AccountAuthResponse> Handle(AccountAuthRequest request)
            {
                // More hardcoded garbage. You'll replace this with a database call. Eventually.
                return new AccountAuthResponse
                {
                    AccountDB = new AccountDB
                    {
                        ServerId = request.SessionKey.AccountServerId,
                        Nickname = "佑树",
                        CallNameKatakana = string.Empty,
                        State = AccountState.Normal,
                        Level = 100,
                        RepresentCharacterServerId = 89919579,
                        PublisherAccountId = request.SessionKey.AccountServerId,
                    },
                    AttendanceBookRewards = new System.Collections.Generic.List<AttendanceBookReward>(),
                    RepurchasableMonthlyProductCountDBs = new System.Collections.Generic.List<PurchaseCountDB>(),
                    MonthlyProductParcel = new System.Collections.Generic.List<ParcelInfo>(),
                    MonthlyProductMail = new System.Collections.Generic.List<ParcelInfo>(),
                    BiweeklyProductParcel = new System.Collections.Generic.List<ParcelInfo>(),
                    BiweeklyProductMail = new System.Collections.Generic.List<ParcelInfo>(),
                    WeeklyProductMail = new System.Collections.Generic.List<ParcelInfo>(),
                    EncryptedUID = "M24ZF7PO3WZ2L7Q23SYITYAZMU",
                    MissionProgressDBs = new System.Collections.Generic.List<MissionProgressDB>()
                };
            }
        }
    }
}
