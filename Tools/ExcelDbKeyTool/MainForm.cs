using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ExcelDbKeyTool;

internal sealed class MainForm : Form
{
    private readonly TextBox protocolInput = new();
    private readonly TextBox rawKeyOutput = new();
    private readonly TextBox pragmaOutput = new();
    private readonly TextBox base64Output = new();
    private readonly Label statusLabel = new();

    public MainForm()
    {
        Text = "ExcelDB Key Decoder";
        MinimumSize = new Size(760, 620);
        Size = new Size(960, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);
        ForeColor = Color.FromArgb(24, 28, 34);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 8,
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var title = new Label
        {
            Text = "ExcelDB Key Decoder",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var inputLabel = new Label
        {
            Text = "Queuing_GetCryptoKeys protocol text",
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        protocolInput.AcceptsReturn = true;
        protocolInput.AcceptsTab = true;
        protocolInput.BorderStyle = BorderStyle.FixedSingle;
        protocolInput.Dock = DockStyle.Fill;
        protocolInput.Font = new Font("Consolas", 9F);
        protocolInput.Multiline = true;
        protocolInput.ScrollBars = ScrollBars.Both;
        protocolInput.WordWrap = false;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 7, 0, 0)
        };
        buttonPanel.Controls.Add(MakeButton("Decode", DecodeProtocol, true));
        buttonPanel.Controls.Add(MakeButton("Clear", ClearAll));

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(inputLabel, 0, 1);
        root.Controls.Add(protocolInput, 0, 2);
        root.Controls.Add(buttonPanel, 0, 3);
        root.Controls.Add(MakeOutputRow("Raw SQLCipher key", rawKeyOutput), 0, 4);
        root.Controls.Add(MakeOutputRow("SQLCipher PRAGMA", pragmaOutput), 0, 5);
        root.Controls.Add(MakeOutputRow("Decrypted key string", base64Output), 0, 6);

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(statusLabel, 0, 7);

        Controls.Add(root);
    }

    private Button MakeButton(string text, Action action, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Width = 112,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(33, 92, 165) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(24, 28, 34),
            Margin = new Padding(0, 0, 8, 0)
        };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(33, 92, 165) : Color.FromArgb(180, 187, 196);
        button.Click += (_, _) => action();
        return button;
    }

    private Control MakeOutputRow(string label, TextBox output)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 5, 0, 0),
            BackColor = BackColor
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var text = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        output.BorderStyle = BorderStyle.FixedSingle;
        output.Dock = DockStyle.Fill;
        output.Font = new Font("Consolas", 9F);
        output.ReadOnly = true;
        output.WordWrap = false;
        output.ScrollBars = ScrollBars.Horizontal;

        panel.Controls.Add(text, 0, 0);
        panel.Controls.Add(MakeButton("Copy", () => CopyOutput(output)), 1, 0);
        panel.Controls.Add(output, 0, 1);
        panel.SetColumnSpan(output, 2);
        return panel;
    }

    private void DecodeProtocol()
    {
        try
        {
            var result = ProtocolKeyDecoder.Decode(protocolInput.Text);
            rawKeyOutput.Text = result.RawSqlCipherKeyHex;
            pragmaOutput.Text = $"PRAGMA key = \"x'{result.RawSqlCipherKeyHex}'\";";
            base64Output.Text = result.DecryptedKeyString;
            SetStatus("Decoded ExcelDB SQLCipher key.", false);
        }
        catch (Exception ex)
        {
            rawKeyOutput.Clear();
            pragmaOutput.Clear();
            base64Output.Clear();
            SetStatus(ex.Message, true);
        }
    }

    private void ClearAll()
    {
        protocolInput.Clear();
        rawKeyOutput.Clear();
        pragmaOutput.Clear();
        base64Output.Clear();
        SetStatus("", false);
    }

    private void CopyOutput(TextBox output)
    {
        if (string.IsNullOrWhiteSpace(output.Text))
            return;

        Clipboard.SetText(output.Text);
        SetStatus("Copied.", false);
    }

    private void SetStatus(string text, bool isError)
    {
        statusLabel.Text = text;
        statusLabel.ForeColor = isError ? Color.FromArgb(170, 35, 35) : Color.FromArgb(35, 110, 55);
    }
}

internal static class ProtocolKeyDecoder
{
    private static readonly string[] RequiredFields =
    [
        "ClientGeneratedKey",
        "ClientGeneratedIV",
        "EncryptedSqlCipherKey"
    ];

    public static DecodeResult Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new InvalidOperationException("Paste the Queuing_GetCryptoKeys request and response text first.");

        var fields = ProtocolFieldExtractor.Extract(input);
        var missing = RequiredFields.Where(field => !fields.ContainsKey(field)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Missing {string.Join(", ", missing)}. Paste both the request fields and the response field; the response alone cannot decrypt the key.");
        }

        var key = DecodeBase64(fields["ClientGeneratedKey"], "ClientGeneratedKey");
        var iv = DecodeBase64(fields["ClientGeneratedIV"], "ClientGeneratedIV");
        var encryptedKey = DecodeBase64(fields["EncryptedSqlCipherKey"], "EncryptedSqlCipherKey");

        if (key.Length is not (16 or 24 or 32))
            throw new InvalidOperationException($"ClientGeneratedKey decoded to {key.Length} bytes; AES needs 16, 24, or 32 bytes.");

        if (iv.Length != 16)
            throw new InvalidOperationException($"ClientGeneratedIV decoded to {iv.Length} bytes; AES-CBC needs 16 bytes.");

        var decrypted = DecryptAesCbc(encryptedKey, key, iv);
        var decryptedText = Encoding.UTF8.GetString(decrypted).Trim();
        var rawSqlCipherKey = DecodeBase64(decryptedText, "decrypted SQLCipher key");

        if (rawSqlCipherKey.Length != 32)
            throw new InvalidOperationException($"The decrypted SQLCipher key decoded to {rawSqlCipherKey.Length} bytes; expected 32 bytes.");

        return new DecodeResult(ToHex(rawSqlCipherKey), decryptedText);
    }

    private static byte[] DecryptAesCbc(byte[] data, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }

    private static byte[] DecodeBase64(string value, string fieldName)
    {
        try
        {
            return Convert.FromBase64String(value.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{fieldName} is not valid Base64.", ex);
        }
    }

    private static string ToHex(byte[] bytes)
    {
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
            hex.Append(value.ToString("x2"));

        return hex.ToString();
    }
}

internal static class ProtocolFieldExtractor
{
    private static readonly Regex FieldRegex = new(
        @"[""']?(ClientGeneratedKey|ClientGeneratedIV|EncryptedSqlCipherKey)[""']?\s*:\s*[""']((?:\\.|[^""'\\])*)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static Dictionary<string, string> Extract(string input)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        ReadJson(input, fields, seen);
        ReadRegex(input, fields);

        var unescaped = TryUnescape(input);
        if (!string.Equals(input, unescaped, StringComparison.Ordinal))
        {
            ReadJson(unescaped, fields, seen);
            ReadRegex(unescaped, fields);
        }

        return fields;
    }

    private static void ReadJson(string text, Dictionary<string, string> fields, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var trimmed = text.Trim();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
            return;

        if (!seen.Add(trimmed))
            return;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            Walk(document.RootElement, fields, seen);
        }
        catch (JsonException)
        {
        }
    }

    private static void Walk(JsonElement element, Dictionary<string, string> fields, HashSet<string> seen)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString() ?? "";
                        TrySet(fields, property.Name, value);
                        ReadJson(value, fields, seen);
                        ReadJson(TryUnescape(value), fields, seen);
                        continue;
                    }

                    Walk(property.Value, fields, seen);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Walk(item, fields, seen);
                break;
        }
    }

    private static void ReadRegex(string text, Dictionary<string, string> fields)
    {
        foreach (Match match in FieldRegex.Matches(text))
        {
            var name = match.Groups[1].Value;
            var value = DecodeJsonString(match.Groups[2].Value);
            TrySet(fields, name, value);
        }
    }

    private static void TrySet(Dictionary<string, string> fields, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!IsWantedField(name))
            return;

        fields.TryAdd(NormalizeName(name), value.Trim());
    }

    private static bool IsWantedField(string name)
    {
        return string.Equals(name, "ClientGeneratedKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ClientGeneratedIV", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "EncryptedSqlCipherKey", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name)
    {
        if (string.Equals(name, "ClientGeneratedKey", StringComparison.OrdinalIgnoreCase))
            return "ClientGeneratedKey";

        if (string.Equals(name, "ClientGeneratedIV", StringComparison.OrdinalIgnoreCase))
            return "ClientGeneratedIV";

        return "EncryptedSqlCipherKey";
    }

    private static string DecodeJsonString(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string>($"\"{value}\"") ?? value;
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string TryUnescape(string value)
    {
        try
        {
            return Regex.Unescape(value);
        }
        catch (ArgumentException)
        {
            return value;
        }
    }
}

internal sealed record DecodeResult(string RawSqlCipherKeyHex, string DecryptedKeyString);

internal static class ProtocolKeyDecoderSelfTest
{
    public static int Run()
    {
        try
        {
            var key = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
            var iv = Enumerable.Range(17, 16).Select(value => (byte)value).ToArray();
            var rawSqlCipherKey = Enumerable.Range(64, 32).Select(value => (byte)value).ToArray();
            var encryptedSqlCipherKey = EncryptAesCbc(Encoding.UTF8.GetBytes(Convert.ToBase64String(rawSqlCipherKey)), key, iv);

            var request = JsonSerializer.Serialize(new
            {
                Protocol = 50001,
                ClientGeneratedKey = Convert.ToBase64String(key),
                ClientGeneratedIV = Convert.ToBase64String(iv)
            });
            var response = JsonSerializer.Serialize(new
            {
                Protocol = 50001,
                EncryptedSqlCipherKey = Convert.ToBase64String(encryptedSqlCipherKey)
            });
            var logText = JsonSerializer.Serialize(new[]
            {
                new { protocol = "Queuing_GetCryptoKeys", packet = request },
                new { protocol = "Queuing_GetCryptoKeys", packet = response }
            });

            var result = ProtocolKeyDecoder.Decode(logText);
            var expectedHex = string.Concat(rawSqlCipherKey.Select(value => value.ToString("x2")));
            return string.Equals(result.RawSqlCipherKeyHex, expectedHex, StringComparison.Ordinal) ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static byte[] EncryptAesCbc(byte[] data, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }
}
