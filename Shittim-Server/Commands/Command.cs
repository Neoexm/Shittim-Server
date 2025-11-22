using Shittim.Services.Client;
using Serilog;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Shittim.Commands
{
    public abstract class Command
    {
        protected IClientConnection connection;
        protected string[] args;

        public Command(IClientConnection connection, string[] args, bool validate = true)
        {
            this.connection = connection;
            this.args = args;
        }

        public string? Validate()
        {
            List<PropertyInfo> argsProperties = GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.GetCustomAttribute(typeof(ArgumentAttribute)) is not null).ToList();
            if (argsProperties.Where(x => (((ArgumentAttribute)x.GetCustomAttribute(typeof(ArgumentAttribute))!).Flags & ArgumentFlags.Optional) != ArgumentFlags.Optional).Count() > args.Length)
                return "Invalid args length!";

            foreach (var argProp in argsProperties)
            {
                ArgumentAttribute attr = (ArgumentAttribute)argProp.GetCustomAttribute(typeof(ArgumentAttribute))!;

                if ((attr.Flags & ArgumentFlags.Remaining) == ArgumentFlags.Remaining)
                {
                    if (attr.Position < args.Length)
                    {
                        var remainingArgs = string.Join(" ", args.Skip(attr.Position));
                        if (!attr.Pattern.IsMatch(remainingArgs))
                            return $"Argument {argProp.Name} is invalid!";
                        argProp.SetValue(this, remainingArgs);
                    }
                    continue;
                }

                if (attr.Position + 1 > args.Length && (attr.Flags & ArgumentFlags.Optional) != ArgumentFlags.Optional)
                    return $"Argument {argProp.Name} is required!";
                else if (attr.Position + 1 > args.Length)
                    return null;

                if (!attr.Pattern.IsMatch(args[attr.Position]))
                    return $"Argument {argProp.Name} is invalid!";

                argProp.SetValue(this, args[attr.Position]);
            }
            return null;
        }

        public abstract Task Execute();
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ArgumentAttribute : Attribute
    {
        public int Position { get; }
        public Regex Pattern { get; set; }
        public string? Description { get; }
        public ArgumentFlags Flags { get; }

        public ArgumentAttribute(int position, string pattern, string? description = null, ArgumentFlags flags = ArgumentFlags.None)
        {
            Position = position;

            if ((flags & ArgumentFlags.IgnoreCase) != ArgumentFlags.IgnoreCase)
                Pattern = new(pattern);
            else
                Pattern = new(pattern, RegexOptions.IgnoreCase);

            Description = description;
            Flags = flags;
        }
    }

    public enum ArgumentFlags
    {
        None = 0,
        Optional = 1,
        IgnoreCase = 2,
        Remaining = 4
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CommandHandlerAttribute : Attribute
    {
        public string Name { get; }

        public string Hint { get; }

        public string Usage { get; }

        public CommandHandlerAttribute(string name, string hint, string usage)
        {
            Name = name;
            Hint = hint;
            Usage = usage;
        }
    }

    public static class CommandFactory
    {
        public static readonly Dictionary<string, Type> commands = new();

        public static void LoadCommands()
        {
            Log.Information("Loading commands...");

            IEnumerable<Type> classes = from t in Assembly.GetExecutingAssembly().GetTypes()
                                        where t.IsClass && t.GetCustomAttribute<CommandHandlerAttribute>() is not null
                                        select t;

            foreach (var command in classes)
            {
                CommandHandlerAttribute nameAttr = command.GetCustomAttribute<CommandHandlerAttribute>()!;
                commands.Add(nameAttr.Name, command);
#if DEBUG
                Log.Information($"Loaded {nameAttr.Name} command");
#endif
            }

            Log.Information("Finished loading commands");
        }

        public static Command? CreateCommand(string name, IClientConnection connection, string[] args, bool validate = true)
        {
            Type? command = commands.GetValueOrDefault(name);
            if (command is null)
                return null;

            var cmd = (Command)Activator.CreateInstance(command, new object[] { connection, args, validate })!;

            string? ret = cmd.Validate();
            if (ret is not null && validate)
            {
                throw new ArgumentException(ret);
            }

            return cmd;
        }
    }
}
