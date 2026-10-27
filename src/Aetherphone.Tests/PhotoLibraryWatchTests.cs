using Aetherphone.Core.Photos;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoLibraryWatchTests : IDisposable
{
    private readonly DirectoryInfo root;
    private readonly PhotoLibrary library;

    public PhotoLibraryWatchTests()
    {
        root = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aetherphone-photowatch-" + Guid.NewGuid().ToString("N")));
        root.Create();
        library = new PhotoLibrary(root);
    }

    public void Dispose()
    {
        if (root.Exists)
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void NothingChangedRightAfterMarking()
    {
        var watch = new PhotoLibraryWatch();
        watch.Mark(library);

        Assert.False(watch.Changed(library));
    }

    [Fact]
    public void ALibraryChangeIsReportedOnce()
    {
        var watch = new PhotoLibraryWatch();
        watch.Mark(library);
        library.MarkChanged();

        Assert.True(watch.Changed(library));
        Assert.False(watch.Changed(library));
    }

    [Fact]
    public void DeletingAPhotoIsReported()
    {
        var photo = CreatePhoto("AEP_20261025_101500_000.png");
        var watch = new PhotoLibraryWatch();
        watch.Mark(library);

        library.Delete(photo);

        Assert.True(watch.Changed(library));
        Assert.Empty(library.List());
    }

    [Fact]
    public void RecoveringAPhotoIsReported()
    {
        library.Delete(CreatePhoto("AEP_20261025_101500_000.png"));
        var trashed = Assert.Single(library.ListTrash());
        var watch = new PhotoLibraryWatch();
        watch.Mark(library);

        var recovered = library.Restore(trashed);

        Assert.True(watch.Changed(library));
        Assert.Equal(recovered, Assert.Single(library.List()));
    }

    [Fact]
    public void AChangeBeforeMarkingIsNotReported()
    {
        var watch = new PhotoLibraryWatch();
        library.MarkChanged();
        watch.Mark(library);

        Assert.False(watch.Changed(library));
    }

    private string CreatePhoto(string name)
    {
        var path = Path.Combine(library.DirectoryPath, name);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        return path;
    }
}
