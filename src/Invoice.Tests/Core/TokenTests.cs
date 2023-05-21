using Invoice.Core;
using Moq;

namespace Invoice.Tests.Core;

public class TokenTests
{
    [Fact]
    public void Create_WithNowType_ReturnsDateTokenWithCurrentDateTime()
    {
        // Arrange
        var date = new DateTimeOffset(2023, 4, 20, 16, 20, 0, TimeSpan.FromHours(-3));
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        mockDateTimeProvider.Setup(provider => provider.Now).Returns(date);
        const string raw = "{{date:now}}";
        const string name = "date";
        const string type = "now";
        const string parameters = "";

        // Act
        var result = Token.Create(raw, name, type, parameters, mockDateTimeProvider.Object);

        // Assert
        Assert.IsType<DateToken>(result);
        Assert.Equal(raw, result.RawToken);
        Assert.Equal(name, result.Key);
        Assert.True(date.EqualsExact(DateTimeOffset.Parse((string)((DateToken)result).Replacement!)));
    }

    [Fact]
    public void Create_WithDateType_ReturnsDateToken()
    {
        // Arrange
        const string raw = "{{date:date(\"dd-MMM-yy\")}}";
        const string name = "date";
        const string type = "date";
        const string parameters = "dd-MMM-yy";

        // Act
        var result = Token.Create(raw, name, type, parameters);

        // Assert
        Assert.IsType<DateToken>(result);
        Assert.Equal(raw, result.RawToken);
        Assert.Equal(name, result.Key);
        Assert.Equal(parameters, ((DateToken)result).Format);
    }

    [Fact]
    public void Create_WithIncType_ReturnsIncrementToken()
    {
        // Arrange
        const string raw = "{{counter:inc}}";
        const string name = "counter";
        const string type = "inc";
        const string parameters = "";

        // Act
        var result = Token.Create(raw, name, type, parameters);

        // Assert
        Assert.IsType<IncrementToken>(result);
        Assert.Equal(raw, result.RawToken);
        Assert.Equal(name, result.Key);
    }

    [Fact]
    public void Create_WithUnknownType_ReturnsStringToken()
    {
        // Arrange
        const string raw = "{{unknown:type}}";
        const string name = "unknown";
        const string type = "type";
        const string parameters = "";

        // Act
        var result = Token.Create(raw, name, type, parameters);

        // Assert
        Assert.IsType<StringToken>(result);
        Assert.Equal(raw, result.RawToken);
        Assert.Equal(name, result.Key);
    }

    // Additional tests for other scenarios can be added here
}