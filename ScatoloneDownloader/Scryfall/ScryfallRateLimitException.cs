using System.Net;

namespace ScatoloneDownloader.Scryfall
{
    /// <summary>
    /// Scryfall still answered 429 after every retry of <see cref="ScryfallClient"/>.
    /// Unlike a missing image or a 5xx, this is not one card's problem: the next
    /// request would be refused too, and pressing on is what gets an IP banned, so
    /// the download loop lets it end the run instead of logging it per card.
    /// </summary>
    internal sealed class ScryfallRateLimitException : HttpRequestException
    {
        internal ScryfallRateLimitException(string url)
            : base(string.Format("Unable to contact: {0}. Status code: {1}", url, HttpStatusCode.TooManyRequests), null, HttpStatusCode.TooManyRequests)
        {
        }
    }
}
