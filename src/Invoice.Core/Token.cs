using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Invoice.Core;

internal abstract record Token
{
    public readonly string RawToken;
    
    public object? Replacement { get; protected set; } = null;

    public string Key { get; }

    public bool IsCancelled { get; private set; } = false;


    [MemberNotNullWhen(false, nameof(Replacement))]
    public bool NeedsValue => Replacement is null;

    protected Token(string rawToken, string key)
    {
        Key = key;
        RawToken = rawToken;
    }

    public static Token Create(string raw, string name, string type, string parameters, IDateTimeProvider? dateTimeProvider = null)
    {
        switch (type.ToLowerInvariant())
        {
            case "date":
                return new DateToken(raw, name, parameters);

            case "now":
                dateTimeProvider ??= DateTimeProvider.Instance;

                var token = new DateToken(raw, name, parameters);
                token.SetDate(dateTimeProvider.Now);

                return token;

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

    public void Cancel()
    {
        IsCancelled = true;
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