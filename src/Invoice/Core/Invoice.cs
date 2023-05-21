using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using HtmlAgilityPack;
using Spectre.Console;

namespace Invoice.Core;

internal delegate Task TokenReplacementDelegate(Token token, CancellationToken cancellationToken);

internal sealed class Invoice
{
    private static readonly Regex TokenRegex = new Regex("{{([\\w-]+)(?::([\\w-]+)(?:\\(\"([\\s\\w-]+)\"\\))?)?}}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public string FilePath { get; }


    private readonly IFile _file;
    private readonly IDateTimeProvider _dateTimeProvider;

    public Invoice(string filePath, IFile file, IDateTimeProvider dateTimeProvider)
    {
        FilePath = filePath;
        _file = file;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<bool> GenerateInvoiceAsync(string outputFilePath, TokenReplacementDelegate replaceToken, CancellationToken cancellationToken = default)
    {
        if (false == _file.Exists(FilePath))
        {
            AnsiConsole.WriteLine();
            throw new FileNotFoundException("Input html file not found", FilePath);
        }

        var inputDocument = new HtmlDocument();
        LoadDocumentFromFile(document: inputDocument, inputFilePath: FilePath);

        var tokenNodes = inputDocument.DocumentNode.SelectNodes(XPathExpression.Compile("//text()[contains(., '{{') and contains(., '}}')]"));
        // Token nodes is null if nothing matches the XPATH expression above
        if (tokenNodes is not null)
        {
            var lineBuilder = new StringBuilder();
            foreach (var tokenizationNode in tokenNodes)
            {
                lineBuilder.Clear();
                lineBuilder.Append(tokenizationNode.InnerText);

                // Create token
                var matches = TokenRegex.Matches(tokenizationNode.InnerText);

                foreach (Match match in matches)
                {
                    // Get first capture group, containing inner text without brackets
                    var rawValue = match.Groups[0].Value;
                    var name = match.Groups[1].Value;
                    var type = match.Groups[2].Value;
                    var parameters = match.Groups[3].Value;

                    var token = Token.Create(rawValue, name, type, parameters, _dateTimeProvider);

                    // Only replace token if needs value
                    if (token.NeedsValue)
                    {
                        await replaceToken(token, cancellationToken);
                    }

                    // Token input was cancelled, so we cancel the 
                    if (token.IsCancelled)
                    {
                        AnsiConsole.WriteLine("Cancelled execution!");
                        return false;
                    }

                    // Token was ignored >:(
                    if (token.NeedsValue)
                    {
                        throw new InvalidOperationException("Token still needs value!");
                    }

                    // Replace token in built line
                    token.Replace(lineBuilder);
                }

                // Replace node in html
                tokenizationNode.ParentNode.ReplaceChild(HtmlNode.CreateNode(lineBuilder.ToString()), tokenizationNode);
            }
        }

        // Save
        SaveDocumentToFile(document: inputDocument,  outputFilePath: outputFilePath);

        return true;
    }

    private void LoadDocumentFromFile(HtmlDocument document, string inputFilePath)
    {
        using var textFile = _file.OpenText(inputFilePath);
        document.Load(textFile);
    }

    private void SaveDocumentToFile(HtmlDocument document, string outputFilePath)
    {
        using var fileStream = _file.CreateText(outputFilePath);
        document.Save(fileStream);
    }
}