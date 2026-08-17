namespace DCT_SD.Helpers.Exceptions;

public class ForbiddenAppException : Exception
{
    public ForbiddenAppException(string message) : base(message) { }
}
