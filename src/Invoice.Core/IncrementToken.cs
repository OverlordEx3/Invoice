namespace Invoice.Core;

internal sealed record IncrementToken : Token
{
    public IncrementToken(string rawToken, string key) : base(rawToken, key) { }

    public void Increment(int value)
    {
        SetReplacement((object)value);
    }
}