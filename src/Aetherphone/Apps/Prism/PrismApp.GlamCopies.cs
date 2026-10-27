using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private void DrainRemovals()
    {
        while (glams.TryTakeRemoval(out var removal))
        {
            if (string.Equals(removal.Kind, ContentRemovalKinds.Look, StringComparison.Ordinal))
            {
                store.PurgeLook(removal.ContentId, removal.ParentId);
                continue;
            }

            PurgeGlam(removal.ContentId);
        }
    }

    private void PurgeGlam(string glamId)
    {
        if (glams.Detail?.Id == glamId)
        {
            photoViewer.Close();
        }

        glams.Purge(glamId);
        DropShowcase(glamId);
        DropHistory(glamId);
        tagGlams = CopyOnWrite.RemoveById(tagGlams, glamId);
        if (profileCover?.Id == glamId)
        {
            profileCover = null;
            coverItems = null;
        }

        if (editBannerGlam?.Id == glamId)
        {
            editBannerGlam = null;
        }

        if (editingGlam?.Id == glamId)
        {
            editingGlam = null;
            if (screen == PrismScreen.Compose)
            {
                GoBack();
            }
        }

        if (cropGlam?.Id == glamId)
        {
            cropGlam = null;
            if (screen == PrismScreen.BannerCrop)
            {
                GoBack();
            }
        }
    }

    private void RefreshGlamCopies(GlamDto saved)
    {
        for (var index = 0; index < showcaseCount; index++)
        {
            if (showcase[index].Id == saved.Id)
            {
                showcase[index] = saved;
            }
        }

        for (var index = 0; index < historyDepth; index++)
        {
            if (history[index].Glam?.Id == saved.Id)
            {
                history[index] = history[index] with { Glam = saved };
            }
        }

        tagGlams = CopyOnWrite.Replace(tagGlams, saved);
        if (profileCover?.Id == saved.Id)
        {
            profileCover = saved;
        }

        if (editBannerGlam?.Id == saved.Id)
        {
            editBannerGlam = saved;
        }
    }

    private void DropHistory(string glamId)
    {
        var kept = 0;
        for (var index = 0; index < historyDepth; index++)
        {
            if (history[index].Screen == PrismScreen.GlamDetail && history[index].Glam?.Id == glamId)
            {
                continue;
            }

            history[kept++] = history[index];
        }

        for (var index = kept; index < historyDepth; index++)
        {
            history[index] = default;
        }

        historyDepth = kept;
    }
}
