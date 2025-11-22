using Shittim_Server.Managers;
using Shittim_Server.Services;
using BlueArchiveAPI.Services;

namespace Shittim.Managers
{
    public static class ManagerExtensions
    {
        public static void AddManagers(this IServiceCollection services)
        {
            services.AddSingleton<CafeManager>();
            services.AddSingleton<CampaignManager>();
            services.AddSingleton<CharacterManager>();
            services.AddSingleton<ConcentrateCampaignManager>();
            services.AddSingleton<EchelonManager>();
            services.AddSingleton<EliminateRaidManager>();
            services.AddSingleton<EquipmentManager>();
            services.AddSingleton<GearManager>();
            services.AddSingleton<ItemManager>();
            services.AddSingleton<MailManager>();
            services.AddSingleton<RaidManager>();
            services.AddSingleton<ScenarioManager>();
            services.AddSingleton<SchoolDungeonManager>();
            services.AddSingleton<ShopManager>();
            services.AddSingleton<TimeAttackDungeonManager>();
            services.AddSingleton<WeekDungeonManager>();
            services.AddSingleton<WorldRaidManager>();
            services.AddSingleton<EventContentCampaignManager>();
        }
    }
}
