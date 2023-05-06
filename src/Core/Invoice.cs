using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using HtmlAgilityPack;

namespace invoice.Core;

internal sealed class Invoice
{
    private static readonly Regex TokenRegex = new Regex("{{([\\w-]+)(?::([\\w-]+)(?:\\(\"([\\s\\w-]+)\"\\))?)?}}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);


    public string FilePath { get; }

    public Invoice(string filePath)
    {
        FilePath = filePath;
    }

    public Task<bool> GenerateInvoiceAsync(string outputFilePath, Action<Token> replaceToken, CancellationToken cancellationToken = default)
    {
        var inputDocument = new HtmlDocument();

        LoadDocumentFromFile(document: inputDocument, inputFilePath: FilePath);

        foreach (var tokenizationNode in inputDocument.DocumentNode.SelectNodes(XPathExpression.Compile("//text()[contains(., '{{') and contains(., '}}')]")))
        {
            var lineBuilder = new StringBuilder(tokenizationNode.InnerText);
            // Create token
            var matches = TokenRegex.Matches(tokenizationNode.InnerText);

            foreach (Match match in matches)
            {
                // Get first capture group, containing inner text without brackets
                var rawValue = match.Groups[0].Value;
                var name = match.Groups[1].Value;
                var type = match.Groups[2].Value;
                var parameters = match.Groups[3].Value;
                
                var token = Token.Create(rawValue, name, type, parameters);

                replaceToken(token);

                if (token.NeedsValue)
                {
                    throw new InvalidOperationException("Token still needs value!");
                }
            }
            
            // Replace value
            tokenizationNode.ParentNode.ReplaceChild(HtmlNode.CreateNode(lineBuilder.ToString()), tokenizationNode);
        }

        // Save
        SaveDocumentToFile(document: inputDocument,  outputFilePath: outputFilePath);

        return Task.FromResult(false);
    }

    private static void LoadDocumentFromFile(HtmlDocument document, string inputFilePath)
    {
        using var textFile = File.OpenText(inputFilePath);
        document.Load(textFile);
    }

    private static void SaveDocumentToFile(HtmlDocument document, string outputFilePath)
    {
        using var fileStream = File.CreateText(outputFilePath);
        document.Save(fileStream);
    }
}

internal abstract class Token
{
    protected string RawToken;
    
    protected object? Replacement = null;

    public string Key { get; }


    [MemberNotNullWhen(false, nameof(Replacement))]
    public bool NeedsValue => Replacement is null;

    protected Token(string rawToken, string key)
    {
        Key = key;
        RawToken = rawToken;
    }

    public static Token Create(string raw, string name, string type, string parameters)
    {
        switch (type.ToLowerInvariant())
        {
            case "now":
                var token = new DateToken(raw, name, parameters);
                token.SetDate(DateTimeOffset.Now);
                return token;

            case "date":
                return new DateToken(raw, name, parameters);
                
            case "inc":
                return new IncrementToken(raw, name);

            default:
                return new StringToken(raw, name);
            
        }
    }

    protected void SetReplacement(object replacement)
    {
        if (NeedsValue)
        {
            Replacement = replacement;
        }
    }

    public void Replace(StringBuilder stringBuilder)
    {
        if (NeedsValue)
        {
            throw new InvalidOperationException("Cannot replace value not set but needed");
        }

        stringBuilder.Replace(RawToken, Replacement.ToString());
    }
}

internal sealed class StringToken : Token
{
    public StringToken(string rawToken, string key) : base(rawToken, key) { }

    public void SetValue(string value)
    {
        SetReplacement((object)value);
    }
}

internal sealed class IncrementToken : Token
{
    public IncrementToken(string rawToken, string key) : base(rawToken, key) { }

    public void Increment(int value)
    {
        SetReplacement((object)value);
    }
}

internal sealed class DateToken : Token
{
    private readonly string? _format = null;

    public DateToken(string rawToken, string key, string? format = default) : base(rawToken, key)
    {
        _format = format;
    }

    public void SetDate(DateTimeOffset value)
    {
        var date = value.ToString(_format);
        SetReplacement((object)date);
    }
}
