using System.Globalization;

namespace Aetherphone.Core.Collections;

internal static class OrchestrionOrder
{
    private const long UnnumberedOffset = 100_000L;

    public static long Key(CollectionItemDto dto, int categoryOrder)
    {
        if (int.TryParse(dto.Number, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return GameOrderKey.Of(categoryOrder, number);
        }

        return GameOrderKey.Of(categoryOrder, UnnumberedOffset + dto.Id);
    }
}
