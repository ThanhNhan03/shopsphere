using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using ShopSphere.SharedKernel;
using System.Net;
using System.Text.RegularExpressions;

namespace ShopSphere.Catalog.Infrastructure;
public sealed class ProductImages : IDisposable
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private readonly AmazonS3Client client;
    private readonly string bucket;
    public ProductImages(IConfiguration configuration)
    {
        bucket = configuration["S3:Bucket"] ?? "shopsphere-products";
        client = new AmazonS3Client(new BasicAWSCredentials(configuration["S3:AccessKey"] ?? "shopsphere", configuration["S3:SecretKey"] ?? "shopsphere-minio-local"),
            new AmazonS3Config { ServiceURL = configuration["S3:Endpoint"] ?? "http://localhost:9000", ForcePathStyle = true, AuthenticationRegion = configuration["S3:Region"] ?? "us-east-1" });
    }
    public static string Validate(byte[] bytes, string contentType)
    {
        Guard.Require(bytes.Length is > 0 and <= MaxBytes, "Choose an image up to 5 MB.");
        if (contentType == "image/png" && bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (contentType == "image/jpeg" && bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "jpg";
        if (contentType == "image/webp" && bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "webp";
        throw new ApiException(400, "The file must be a valid PNG, JPEG or WebP image. SVG files are not accepted.");
    }
    public async Task<string> Store(byte[] bytes, string contentType, CancellationToken ct)
    {
        var key = $"{Guid.NewGuid():N}.{Validate(bytes, contentType)}";
        try
        {
            try { await client.GetBucketLocationAsync(bucket, ct); }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound || e.ErrorCode == "NoSuchBucket")
            {
                try { await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct); }
                catch (AmazonS3Exception conflict) when (conflict.ErrorCode == "BucketAlreadyOwnedByYou") { }
            }
            using var stream = new MemoryStream(bytes);
            await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = "products/" + key, InputStream = stream, ContentType = contentType, UseChunkEncoding = false }, ct);
            return "/api/media/" + key;
        }
        catch (AmazonS3Exception) { throw new ApiException(503, "Image storage is temporarily unavailable. Please retry the upload."); }
        catch (HttpRequestException) { throw new ApiException(503, "Image storage is temporarily unavailable. Please retry the upload."); }
    }
    public async Task<GetObjectResponse> Read(string key, CancellationToken ct)
    {
        if (!Regex.IsMatch(key, "^[a-f0-9]{32}\\.(png|jpg|webp)$")) throw new ApiException(404, "Image not found.");
        try { return await client.GetObjectAsync(bucket, "products/" + key, ct); }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { throw new ApiException(404, "Image not found."); }
        catch (AmazonS3Exception) { throw new ApiException(503, "Image storage is temporarily unavailable."); }
        catch (HttpRequestException) { throw new ApiException(503, "Image storage is temporarily unavailable."); }
    }
    public async Task Delete(string url, CancellationToken ct)
    {
        if (!url.StartsWith("/api/media/", StringComparison.Ordinal)) return;
        await client.DeleteObjectAsync(bucket, "products/" + url.Split('/').Last(), ct);
    }
    public void Dispose() => client.Dispose();
}
