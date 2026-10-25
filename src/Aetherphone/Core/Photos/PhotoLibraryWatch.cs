namespace Aetherphone.Core.Photos;

internal struct PhotoLibraryWatch
{
    private int seenVersion;

    public void Mark(PhotoLibrary library) => seenVersion = library.Version;

    public bool Changed(PhotoLibrary library)
    {
        var current = library.Version;
        if (current == seenVersion)
        {
            return false;
        }

        seenVersion = current;
        return true;
    }
}
