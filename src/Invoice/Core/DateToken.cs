namespace Invoice.Core;

internal sealed record DateToken : Token
{
    public readonly string? Format;

    public DateToken(string rawToken, string key, string? format = default) : base(rawToken, key)
    {
        Format = format;
    }

    public void SetDate(DateTimeOffset value)
    {
        var date = value.ToString(Format);
        SetReplacement((object)date);
    }
}