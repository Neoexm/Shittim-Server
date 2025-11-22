using Microsoft.EntityFrameworkCore;
using Shittim.Services.Client;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Shittim.Commands
{
    [CommandHandler("mail", "Command to sending mail to player", "/mail [type] [id,...] [amount]")]
    internal class MailCommand : Command
    {
        public MailCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @".*", "Mail Type or 'help'", ArgumentFlags.None)]
        public string TypeStr { get; set; }

        [Argument(1, @".*", "Mail id(s) list", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string IdStr { get; set; }

        [Argument(2, @".*", "amount", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string AmountStr { get; set; } = "1";

        public record ItemKey(ParcelType Type, long Id);
        private static Dictionary<ItemKey, object> _itemDict;
        private static bool _initialized = false;

        public override async Task Execute()
        {
            InitializeIndex();
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);
            if (args == null || args.Length == 0)
            {
                await connection.SendChatMessage("Usage: /mail [type] [id,...] [amount]");
                return;
            }

            var typeToken = args[0].Trim();
            if (string.Equals(typeToken, "help", StringComparison.OrdinalIgnoreCase))
            {
                await ShowHelp();
                return;
            }
        
            if (string.Equals(typeToken, "clear", StringComparison.OrdinalIgnoreCase))
            {
                int affectedRows = await context.Mails.Where(x => x.AccountServerId == account.ServerId && x.ReceiptDate == null)
                    .ExecuteDeleteAsync();
                if (affectedRows > 0)
                    await connection.SendChatMessage($"Deleted {affectedRows} unread mail");
                else
                    await connection.SendChatMessage("No emails to delete");

                return;
            }

            long amount = 1;
            long parsedAmount = 1;
            bool hasAmount = args.Length >= 3 && long.TryParse(args[args.Length - 1], out parsedAmount);
            if (hasAmount)
                amount = parsedAmount;

            int idEndExclusive = hasAmount ? args.Length - 1 : args.Length;
            var idTokens = args.Skip(1).Take(Math.Max(0, idEndExclusive - 1));
            string idConcat = string.Concat(idTokens);

            if (string.IsNullOrWhiteSpace(idConcat))
            {
                await connection.SendChatMessage("Error: Please enter the correct type and id");
                return;
            }

            List<long> ids;
            try
            {
                ids = idConcat
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => long.Parse(s.Trim()))
                    .ToList();
            }
            catch (FormatException)
            {
                await connection.SendChatMessage("Error: Invalid id format.");
                return;
            }

            _ = long.TryParse(TypeStr, out var type);
            ParcelType parcelType = (ParcelType)(int)type;
            List<ParcelInfo> parcelInfos = new();
            foreach (var id in ids)
            {
                var key = new ItemKey(parcelType, id);
                if (!_itemDict.TryGetValue(key, out var obj))
                {
                    await connection.SendChatMessage("Error: Please enter the correct type and id");
                    return;
                }
                parcelInfos.AddRange(ParcelInfo.CreateParcelInfo(parcelType, id, amount));
            }

            context.Mails.Add(new()
            {
                AccountServerId = account.ServerId,
                Type = MailType.System,
                UniqueId = 1,
                Sender = "Schale",
                Comment = "Items sent by GM",
                SendDate = DateTime.Now,
                ExpireDate = DateTime.Now.AddDays(7),
                ParcelInfos = parcelInfos,
                RemainParcelInfos = new()
            });

            await connection.SendChatMessage("Send mail successfully, please check your mailbox");
            await connection.SendChatMessage("If the client abnormal after sending email, please use '/mail clear' to fix it.");
            await context.SaveChangesAsync();
        }

        private void InitializeIndex()
        {
            if (!_initialized)
            {
                _itemDict = new Dictionary<ItemKey, object>();

                var currencyExcel = connection.ExcelTableService.GetTable<CurrencyExcelT>();
                foreach (var x in currencyExcel)
                    _itemDict[new ItemKey(ParcelType.Currency, x.ID)] = x;

                var itemExcel = connection.ExcelTableService.GetTable<ItemExcelT>();
                foreach (var x in itemExcel)
                    _itemDict[new ItemKey(ParcelType.Item, x.Id)] = x;

                var equipmentExcel = connection.ExcelTableService.GetTable<EquipmentExcelT>();
                foreach (var x in equipmentExcel)
                    _itemDict[new ItemKey(ParcelType.Equipment, x.Id)] = x;
                _initialized = true;

                var furnitureExcel = connection.ExcelTableService.GetTable<FurnitureExcelT>();
                foreach (var x in furnitureExcel)
                    _itemDict[new ItemKey(ParcelType.Furniture, x.Id)] = x;

                _initialized = true;
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/mail - Command to sending mail to player");
            await connection.SendChatMessage("Usage: /mail [type] [id,...] [amount]");
            await connection.SendChatMessage("Type: currency | equipment | item |  furniture - 2/3/4/13");
            await connection.SendChatMessage("Support sending multiple items of the same type, use ',' to separate each ID");
            await connection.SendChatMessage("You can find item ID at schaledb.com");
            await connection.SendChatMessage("If the client abnormal after sending email, use '/mail clear' to fix it.");
        }
    }
}
