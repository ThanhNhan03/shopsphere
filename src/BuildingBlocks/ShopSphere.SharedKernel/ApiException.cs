namespace ShopSphere.SharedKernel;

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class Guard
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new ApiException(400, message);
    }
}
