namespace Invoice.Core;

internal sealed record StringToken : Token
{
    public StringToken(string rawToken, string key) : base(rawToken, key) { }

    public void SetValue(string value)
    {
        SetReplacement((object)value);
    }
}