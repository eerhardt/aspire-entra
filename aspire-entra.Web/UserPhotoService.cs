using System.Net;
using System.Net.Mime;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Web;

namespace aspire_entra.Web;

/// <summary>
/// Gets the signed-in user's profile photo from Microsoft Graph as a data URL that can be used as an image source.
/// </summary>
public class UserPhotoService(GraphServiceClient graphClient, IMemoryCache cache)
{
    // The DownstreamApis entry the AppHost configures from the frontend's Microsoft Graph API permissions.
    public const string GraphConfigurationSection = "DownstreamApis:MicrosoftGraph";

    // One of the sizes Microsoft Graph stores for profile photos. A small size keeps the page light.
    private const string PhotoSize = "48x48";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the user's photo as a data URL, or <see langword="null"/> when the user has no photo.
    /// </summary>
    public async Task<string?> GetPhotoDataUrlAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var cacheKey = (nameof(UserPhotoService), user.GetObjectId());

        // Users without a photo are cached too, so Graph isn't called on every page for them.
        return await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            try
            {
                using var photo = await graphClient.Me.Photos[PhotoSize].Content.GetAsync(cancellationToken: cancellationToken);
                if (photo is null)
                {
                    return null;
                }

                using var buffer = new MemoryStream();
                await photo.CopyToAsync(buffer, cancellationToken);
                return $"data:{MediaTypeNames.Image.Jpeg};base64,{Convert.ToBase64String(buffer.ToArray())}";
            }
            catch (ODataError error) when (error.ResponseStatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        });
    }
}
