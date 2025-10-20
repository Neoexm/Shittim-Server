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
                    user = new Models.User
                    {
                        Nickname = "",
                        PublisherAccountId = request.NpSN.ToString(),
                        CreateDate = DateTime.UtcNow,
                        LastConnectTime = DateTime.UtcNow,
                    };
                    _dbContext.Users.Add(user);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    user.LastConnectTime = DateTime.UtcNow;
                    
                    if (user.CreateDate == DateTime.MinValue || user.CreateDate.Year < 2000)
                    {
                        user.CreateDate = DateTime.UtcNow;
                    }
                    
                    await _dbContext.SaveChangesAsync();
                }

                var session = new SessionKey
                {
                    AccountServerId = user.Id,
                    MxToken = Guid.NewGuid().ToString()
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
            private readonly BAContext _dbContext;

            public Auth()
            {
                var options = new DbContextOptionsBuilder<BAContext>()
                    .UseSqlite("Data Source=BlueArchive.db")
                    .Options;
                _dbContext = new BAContext(options);
            }

            public Auth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountAuthResponse> Handle(AccountAuthRequest request)
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                return new AccountAuthResponse
                {
                    CurrentVersion = 0,
                    MinimumVersion = 0,
                    IsDevelopment = false,
                    BattleValidation = false,
                    UpdateRequired = false,
                    TTSCdnUri = "https://ba.dn.nexoncdn.co.kr/tts/version2/",
                    AccountDB = new AccountDB
                    {
                        ServerId = user?.Id ?? request.SessionKey.AccountServerId,
                        Nickname = user?.Nickname ?? "",
                        CallName = user?.Nickname ?? "",
                        State = AccountState.Normal,
                        Level = user?.Level ?? 1,
                        Exp = 0,
                        Comment = "",
                        RepresentCharacterServerId = user?.RepresentCharacterServerId ?? 9,
                        PublisherAccountId = user != null ? long.Parse(user.PublisherAccountId) : 0,
                        LastConnectTime = user?.LastConnectTime ?? DateTime.UtcNow,
                        CreateDate = user?.CreateDate ?? DateTime.UtcNow,
                        BirthDay = DateTime.MinValue,
                        CallNameUpdateTime = DateTime.MinValue,
                    },
                    AttendanceBookRewards = new System.Collections.Generic.List<AttendanceBookReward>(),
                    AttendanceHistoryDBs = new System.Collections.Generic.List<AttendanceHistoryDB>(),
                    RepurchasableMonthlyProductCountDBs = new System.Collections.Generic.List<PurchaseCountDB>(),
                    MonthlyProductParcel = new System.Collections.Generic.List<ParcelInfo>(),
                    MonthlyProductMail = new System.Collections.Generic.List<ParcelInfo>(),
                    BiweeklyProductParcel = new System.Collections.Generic.List<ParcelInfo>(),
                    BiweeklyProductMail = new System.Collections.Generic.List<ParcelInfo>(),
                    WeeklyProductParcel = new System.Collections.Generic.List<ParcelInfo>(),
                    WeeklyProductMail = new System.Collections.Generic.List<ParcelInfo>(),
                    EncryptedUID = string.Empty,
                    AccountRestrictionsDB = new AccountRestrictionsDB(),
                    IssueAlertInfos = new System.Collections.Generic.List<IssueAlertInfoDB>(),
                    MissionProgressDBs = new System.Collections.Generic.List<MissionProgressDB>(),
                    StaticOpenConditions = System.Enum.GetValues(typeof(OpenConditionContent))
                        .Cast<OpenConditionContent>()
                        .ToDictionary(key => key, key => OpenConditionLockReason.None)
                };
            }
        }
    }
}
