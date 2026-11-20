using System.Net;
using System.Net.Mime;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Web;
using Microsoft.Net.Http.Headers;

namespace aspire_entra.Web;

/// <summary>
/// Serves the signed-in user's profile photo from Microsoft Graph, so the browser can cache it like any other image.
/// </summary>
public static class UserPhotoEndpoints
{
    // The DownstreamApis entry the AppHost configures from the frontend's Microsoft Graph API permissions.
    public const string GraphConfigurationSection = "DownstreamApis:MicrosoftGraph";

    public const string PhotoPath = "/user/photo";

    // One of the sizes Microsoft Graph stores for profile photos.
    private const string PhotoSize = "48x48";

    // "private" because the response is specific to the signed-in user: only the browser may cache it, not shared caches.
    private static readonly CacheControlHeaderValue CacheControl = new() { Private = true, MaxAge = TimeSpan.FromHours(1) };

    private static void SetCacheHeaders(HttpResponse response)
    {
        response.GetTypedHeaders().CacheControl = CacheControl;

        // The URL is the same for every user, so the cached photo is tied to the authentication cookie. This keeps a
        // different user signing in on the same browser from seeing the previous user's photo.
        response.Headers.Vary = HeaderNames.Cookie;
    }

    public static IEndpointConventionBuilder MapUserPhoto(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(PhotoPath, async (GraphServiceClient graphClient, HttpResponse response, CancellationToken cancellationToken) =>
        {
            try
            {
                var photo = await graphClient.Me.Photos[PhotoSize].Content.GetAsync(cancellationToken: cancellationToken);

                // Users without a photo get a cached 404 too, so the browser doesn't ask again on every page.
                SetCacheHeaders(response);
                return photo is null ? Results.NotFound() : Results.Stream(photo, MediaTypeNames.Image.Jpeg);
            }
            catch (ODataError error) when (error.ResponseStatusCode == (int)HttpStatusCode.NotFound)
            {
                SetCacheHeaders(response);
                return Results.NotFound();
            }
            catch (MicrosoftIdentityWebChallengeUserException)
            {
                // A Graph token can't be acquired silently, for example after an app restart cleared the in-memory
                // token cache. An image request can't follow a sign-in redirect, so it fails without being cached.
                return Results.Unauthorized();
            }
        });
}
