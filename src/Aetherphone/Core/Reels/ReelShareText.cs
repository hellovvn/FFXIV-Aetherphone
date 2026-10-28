using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Reels;

internal static class ReelShareText
{
    private const int MaxVideoMegabytes = (int)(ReelShareJob.MaxVideoBytes / (1024 * 1024));
    private const int MaxSeconds = (int)ReelFormat.MaxDurationSeconds;

    internal static string Failure(in ReelShareFailure failure) => failure.Step switch
    {
        ReelShareStep.Convert => Loc.T(ForTranscode(failure.Transcode)),
        ReelShareStep.Cover => failure.Transcode is ReelTranscodeFailure.EncodeFailed
            or ReelTranscodeFailure.UnreadableInput
            ? Loc.T(L.Aethergram.ReelErrorCover)
            : Loc.T(ForTranscode(failure.Transcode)),
        ReelShareStep.Upload => Loc.T(L.Aethergram.ReelErrorUpload, FailureText.Resolve(failure.Network)),
        ReelShareStep.Create => Server(failure.Network),
        _ => string.Empty,
    };

    internal static LocString ForTranscode(ReelTranscodeFailure failure) => failure switch
    {
        ReelTranscodeFailure.MediaFoundationMissing => L.Aethergram.ReelErrorNoMediaFoundation,
        ReelTranscodeFailure.EncoderMissing => L.Aethergram.ReelErrorEncoder,
        ReelTranscodeFailure.DecoderMissing => L.Aethergram.ReelErrorDecoder,
        ReelTranscodeFailure.UnreadableInput => L.Aethergram.ReelErrorUnreadable,
        ReelTranscodeFailure.NoVideo => L.Aethergram.ReelErrorNoVideo,
        ReelTranscodeFailure.InvalidRange => L.Aethergram.ReelErrorRange,
        ReelTranscodeFailure.UnknownDuration => L.Aethergram.ReelErrorNoDuration,
        _ => L.Aethergram.ReelErrorConvert,
    };

    internal static string Server(in AepFailure failure)
    {
        switch (failure.Code)
        {
            case ReelErrorCodes.MissingVideo:
                return Loc.T(L.Aethergram.ReelErrorMissingVideo);
            case ReelErrorCodes.TooBig:
                return Loc.T(L.Aethergram.ReelErrorTooBig, MaxVideoMegabytes);
            case ReelErrorCodes.NotMp4:
                return Loc.T(L.Aethergram.ReelErrorNotMp4);
            case ReelErrorCodes.NotFaststart:
                return Loc.T(L.Aethergram.ReelErrorNotFaststart);
            case ReelErrorCodes.HeaderTooLarge:
                return Loc.T(L.Aethergram.ReelErrorHeader);
            case ReelErrorCodes.Fragmented:
                return Loc.T(L.Aethergram.ReelErrorFragmented);
            case ReelErrorCodes.NoVideoTrack:
                return Loc.T(L.Aethergram.ReelErrorNoVideoTrack);
            case ReelErrorCodes.VideoCodec:
                return Loc.T(L.Aethergram.ReelErrorVideoCodec);
            case ReelErrorCodes.AudioCodec:
                return Loc.T(L.Aethergram.ReelErrorAudioCodec);
            case ReelErrorCodes.TooLong:
                return Loc.Plural(L.Aethergram.ReelErrorTooLong, MaxSeconds);
            case ReelErrorCodes.NoDuration:
                return Loc.T(L.Aethergram.ReelErrorServerDuration);
            case ReelErrorCodes.Resolution:
                return Loc.T(L.Aethergram.ReelErrorResolution);
            case ReelErrorCodes.Aspect:
                return Loc.T(L.Aethergram.ReelErrorAspect);
            case ReelErrorCodes.MediaInUse:
                return Loc.T(L.Aethergram.ReelErrorInUse);
            case ReelErrorCodes.PosterInvalid:
                return Loc.T(L.Aethergram.ReelErrorPoster);
            default:
                var text = FailureText.Resolve(failure);
                return text.Length > 0 ? text : Loc.T(L.Account.CannotReach);
        }
    }

    internal static string Status(ReelShareStage stage, float progress, LocString posting)
    {
        var percent = (int)MathF.Round(Math.Clamp(progress, 0f, 1f) * 100f);
        return stage switch
        {
            ReelShareStage.Converting => Loc.T(L.Aethergram.ReelConverting, percent),
            ReelShareStage.Uploading => Loc.T(L.Aethergram.ReelUploading, percent),
            _ => Loc.T(posting),
        };
    }
}
