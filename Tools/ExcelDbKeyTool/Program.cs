using System;
using System.Windows.Forms;

namespace ExcelDbKeyTool;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            return ProtocolKeyDecoderSelfTest.Run() != 0 || MemoryScanSelfTest.Run() != 0 ? 1 : 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
