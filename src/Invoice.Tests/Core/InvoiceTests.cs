
using System.IO.Abstractions;
using System.Text;
using Invoice.Core;
using Moq;

namespace Invoice.Tests.Core;

public class InvoiceTests
{
    private readonly Mock<IFile> _mockFile;

    public InvoiceTests()
    {
        _mockFile = new Mock<IFile>(MockBehavior.Strict);
    }

    private static StreamReader GetStreamReaderForHtml(string html) => new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(html)));

    [Fact]
    public async Task GenerateInvoiceAsync_WithNoTokens_ReturnsTrue()
    {
        // Arrange
        _mockFile.Setup(x => x.Exists("input.html")).Returns(true);
        _mockFile.Setup(x => x.OpenText("input.html")).Returns(GetStreamReaderForHtml("<html><body>hello world</body></html>"));
        _mockFile.Setup(file => file.CreateText("output.html")).Returns(StreamWriter.Null);
        var invoice = new Invoice.Core.Invoice("input.html", _mockFile.Object, DateTimeProvider.Instance);

        // Act
        var result = await invoice.GenerateInvoiceAsync("output.html", async (token, cancellationToken) =>
        {
            // Do nothing
            await Task.CompletedTask;
        });

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GenerateInvoiceAsync_WithNonexistentInputFile_ThrowsFileNotFoundException()
    {
        // Arrange
        _mockFile.Setup(x => x.Exists("input.html")).Returns(false);
        var invoice = new Invoice.Core.Invoice("input.html", _mockFile.Object, DateTimeProvider.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => invoice.GenerateInvoiceAsync("output.html", async (token, cancellationToken) =>
        {
            // Do nothing
            await Task.CompletedTask;
        }));
    }

    [Fact]
    public async Task GenerateInvoiceAsync_WithCancelledToken_ReturnsFalse()
    {
        // Arrange
        _mockFile.Setup(x => x.Exists("input.html")).Returns(true);
        _mockFile.Setup(x => x.OpenText("input.html")).Returns(GetStreamReaderForHtml("<html><body>{{token:cancelled}}</body></html>"));
        var invoice = new Invoice.Core.Invoice("input.html", _mockFile.Object, DateTimeProvider.Instance);
        var cancelledToken = Token.Create("{{token:cancelled}}", "token", "cancelled", "");

        // Act
        var result = await invoice.GenerateInvoiceAsync("output.html", async (token, cancellationToken) =>
        {
            if (token == cancelledToken)
            {
                token.Cancel();
            }

            await Task.CompletedTask;
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GenerateInvoiceAsync_WithTokenThatNeedsValue_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockFile.Setup(x => x.Exists("input.html")).Returns(true);
        _mockFile.Setup(x => x.OpenText("input.html")).Returns(GetStreamReaderForHtml("<html><body>{{token:missingValue}}</body></html>"));
        var invoice = new Invoice.Core.Invoice("input.html", _mockFile.Object, DateTimeProvider.Instance);
        var tokenWithMissingValue = Token.Create("{{token:missingValue}}", "token", "missingValue", "");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => invoice.GenerateInvoiceAsync("output.html", async (token, cancellationToken) =>
        {
            if (token == tokenWithMissingValue)
            {
                // Do nothing, token still needs value
            }

            await Task.CompletedTask;
        }));
    }

    [Fact]
    public async Task GenerateInvoiceAsync_WithValidToken_ReplacesTokenInOutputFile()
    {
        // Arrange
        _mockFile.Setup(x => x.Exists("input.html")).Returns(true);
        _mockFile.Setup(x => x.OpenText("input.html")).Returns(GetStreamReaderForHtml("<html><body>{{name}}</body></html>"));
        var expectedStream = new MemoryStream();
        _mockFile.Setup(file => file.CreateText("output.html")).Returns(new StreamWriter(expectedStream, leaveOpen: true));
        var invoice = new Invoice.Core.Invoice("input.html", _mockFile.Object, DateTimeProvider.Instance);
        var expectedOutput = "<html><body>John Smith</body></html>";

        // Act
        var result = await invoice.GenerateInvoiceAsync("output.html", async (token, cancellationToken) =>
        {
            if (token is StringToken { Key: "name" } stringToken)
            {
                stringToken.SetValue("John Smith");
            }
            else
            {
                Assert.Fail("Token is not valid (not string or with invalid key type)");
            }

            await Task.CompletedTask;
        });

        // Assert
        Assert.True(result);

        _mockFile.Verify(x => x.CreateText("output.html"), Times.Once);
        _mockFile.Verify(x => x.CreateText(It.IsAny<string>()), Times.Once);

        expectedStream.Seek(0, SeekOrigin.Begin);
        var textReader = new StreamReader(expectedStream);
        var outputFileContents = await textReader.ReadToEndAsync();
        Assert.Equal(expectedOutput, outputFileContents);
    }

    [Fact]
    public async Task GenerateInvoiceAsync_WithMultipleValidTokens_ReplacesTokensInOutputFile()
    {
        var nowDate = new DateTimeOffset(2023, 4, 20, 16, 22, 00, TimeSpan.FromHours(-3));
        var dateTime = new DateTimeOffset(1997, 11, 28, 16, 20, 00, TimeSpan.FromHours(-3));
        // Arrange
        _mockFile.Setup(x => x.Exists("input.html")).Returns(true);
        _mockFile.Setup(x => x.OpenText("input.html")).Returns(GetStreamReaderForHtml("<html><body>{{name}}, {{age:inc}}, {{city}}. Today is {{date:Now(\"dd-MMM-yy\")}}. Created at {{created:Date(\"dd-MMM-yy\")}}</body></html>"));

        var expectedStream = new MemoryStream();
        _mockFile.Setup(file => file.CreateText("output.html")).Returns(new StreamWriter(expectedStream, leaveOpen: true));

        var mockDateTimeProvider = new Mock<IDateTimeProvider>(MockBehavior.Strict);
        mockDateTimeProvider.Setup(provider => provider.Now).Returns(nowDate);

        var invoice = new Invoice.Core.Invoice("input.html", _mockFile.Object, mockDateTimeProvider.Object);
        var expectedOutput = "<html><body>John Smith, 42, New York. Today is 20-Apr-23. Created at 28-Nov-97</body></html>";

        // Act
        var _ = await invoice.GenerateInvoiceAsync("output.html", async (token, cancellationToken) =>
        {
            switch (token)
            {
                case StringToken { Key: "name" } str:
                    str.SetValue("John Smith");
                    break;
                case StringToken { Key: "city" } str:
                    str.SetValue("New York");
                    break;
                case IncrementToken { Key: "age" } age:
                    age.Increment(42);
                    break;
                case DateToken { Key: "created" } date:
                    date.SetDate(dateTime);
                    break;
                default:
                    Assert.Fail("Token is invalid");
                    return;
            }

            await Task.CompletedTask;
        });

        // Assert
        _mockFile.Verify(x => x.CreateText("output.html"), Times.Once);
        _mockFile.Verify(x => x.CreateText(It.IsAny<string>()), Times.Once);

        mockDateTimeProvider.Verify(provider => provider.Now, Times.Once);

        expectedStream.Seek(0, SeekOrigin.Begin);
        var textReader = new StreamReader(expectedStream);
        var outputFileContents = await textReader.ReadToEndAsync();
        Assert.Equal(expectedOutput, outputFileContents);
    }

    // Additional tests for other scenarios can be added here
}