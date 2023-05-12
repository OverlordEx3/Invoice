namespace Invoice.Core;

public interface IDateTimeProvider
{
    public DateTimeOffset Now { get; }
}

public sealed class DateTimeProvider
{
    public static readonly IDateTimeProvider Instance = new DefaultTimeProvider();


    private class DefaultTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}