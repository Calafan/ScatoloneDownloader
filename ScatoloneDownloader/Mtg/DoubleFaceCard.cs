using ScatoloneDownloader.Json.Cards;

namespace ScatoloneDownloader.Mtg
{
    internal class DoubleFaceCard : Card
    {
        internal string FrontName { get; init; }
        internal string RearName { get; init; }

        /// <summary>Face image URL, or null when Scryfall lists the face without
        /// its own <c>image_uris</c> block. <see cref="Download.CardDownloader"/>
        /// rejects such a card rather than composing half a picture.</summary>
        internal string? FrontImageUri { get; init; }
        internal string? RearImageUri { get; init; }

        internal DoubleFaceCard(JsonCard jsonCard) : base(jsonCard)
        {
            // Reached only when the top-level image_uris is absent (see
            // Card.CreateCard), which for real Scryfall data means the card
            // carries its art on two faces. Anything else is malformed input.
            if (jsonCard.CardFaces is not { Count: >= 2 })
            {
                throw new ArgumentException(
                    $"Card '{jsonCard.Name}' has no top-level image_uris and fewer than two card_faces.",
                    nameof(jsonCard));
            }

            JsonCardFace front = jsonCard.CardFaces[0];
            JsonCardFace rear = jsonCard.CardFaces[1];

            FrontName = front.Name ?? string.Empty;
            RearName = rear.Name ?? string.Empty;

            Colors = [.. front.Colors ?? [], .. rear.Colors ?? []];

            FrontImageUri = front.ImageUris?.Png;
            RearImageUri = rear.ImageUris?.Png;
        }
    }
}
