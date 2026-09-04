using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
    internal class SingleFaceCard : Card
    {
        /// <summary>Card image URL, or null when the <c>image_uris</c> block
        /// carries no <c>png</c> entry. <see cref="Download.CardDownloader"/>
        /// rejects such a card rather than requesting an empty URL.</summary>
        internal string? ImageUri { get; init; }

        internal SingleFaceCard(JsonCard jsonCard) : base(jsonCard)
        {
            // Only constructed when image_uris is present (see Card.CreateCard),
            // but the png entry inside it is still optional.
            ImageUri = jsonCard.ImageUris?.Png;
        }
    }
}
