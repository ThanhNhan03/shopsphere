using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ShopSphere.Messaging;

public static class ProductionConfiguration
{
    public static void Validate(WebApplicationBuilder builder, string service)
    {
        if (!builder.Environment.IsProduction()) return;
        var config = builder.Configuration;

        if (service is "gateway" or "payment")
        {
            if (!Uri.TryCreate(config["Frontend:Url"], UriKind.Absolute, out var storefront) || storefront.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Production requires Frontend:Url to use HTTPS.");
        }

        if (service is "gateway" or "catalog" or "ordering" or "inventory" or "payment" or "notification")
        {
            var connection = config.GetConnectionString("Database");
            var password = ConnectionValue(connection, "Password", "Pwd");
            var sslMode = ConnectionValue(connection, "SSL Mode", "SslMode");
            if (string.IsNullOrWhiteSpace(password) || password == "shopsphere-local" ||
                !string.Equals(sslMode, "VerifyFull", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Production requires a secret Database password and SSL Mode=VerifyFull.");
        }

        if (service is "catalog" or "ordering" or "inventory" or "payment" or "notification")
        {
            var password = config["RabbitMq:Password"];
            if (string.IsNullOrWhiteSpace(config["RabbitMq:Host"]) || string.IsNullOrWhiteSpace(password) || password == "shopsphere-local" ||
                !config.GetValue<bool>("RabbitMq:UseSsl"))
                throw new InvalidOperationException("Production requires a private RabbitMQ endpoint, TLS, and a non-demo password.");
        }

        if (service == "gateway" && !Path.IsPathRooted(config["Auth:KeyPath"] ?? ""))
            throw new InvalidOperationException("Production requires Auth:KeyPath to point to persistent shared storage.");

        if (service == "basket")
        {
            var redis = config.GetConnectionString("Redis") ?? "";
            if (!redis.Contains("password=", StringComparison.OrdinalIgnoreCase) || !redis.Contains("ssl=true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Production requires an authenticated Redis connection protected with TLS.");
        }

        if (service == "catalog")
        {
            if (!Uri.TryCreate(config["S3:Endpoint"], UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Production requires the S3 endpoint to use HTTPS.");
            var access = config["S3:AccessKey"];
            if (string.IsNullOrWhiteSpace(access) || access == "shopsphere")
                throw new InvalidOperationException("Production requires a non-demo S3 access key from a secret store.");
            var secret = config["S3:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret) || secret == "shopsphere-minio-local")
                throw new InvalidOperationException("Production requires a non-demo S3 secret from a secret store.");
        }

        if (service == "notification" &&
            (!string.Equals(config["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(config["Email:Smtp:Host"]) ||
             string.IsNullOrWhiteSpace(config["Email:Smtp:FromAddress"]) ||
             config["Email:Smtp:Security"] is not ("StartTls" or "SslOnConnect")))
            throw new InvalidOperationException("Production notification delivery requires an SMTP host, sender and TLS mode.");

        if (service == "payment")
        {
            var key = config["Stripe:SecretKey"];
            if (config["Payment:Mode"] != "Stripe" || key is null || !key.StartsWith("sk_live_", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(config["Stripe:WebhookSecret"]))
                throw new InvalidOperationException("Production payments require Stripe live mode and its matching webhook signing secret.");
        }
    }

    private static string? ConnectionValue(string? connectionString, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var values = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (string key in values.Keys)
                if (keys.Contains(key, StringComparer.OrdinalIgnoreCase)) return Convert.ToString(values[key]);
        }
        catch (ArgumentException)
        {
            return null;
        }
        return null;
    }
}
