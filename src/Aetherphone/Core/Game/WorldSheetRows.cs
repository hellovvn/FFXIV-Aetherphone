namespace Aetherphone.Core.Game;

internal readonly record struct WorldSheetRow(uint Id, string Name, uint DataCenterId, bool IsPublic);

internal readonly record struct DataCenterSheetRow(uint Id, string Name, uint RegionId);
