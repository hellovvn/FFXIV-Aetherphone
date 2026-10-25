using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Chirper;

internal enum PollDraftIssue : byte
{
    None,
    EmptyOption,
    OptionTooLong,
    DuplicateOption,
}

internal sealed class ChirperPollDraft
{
    private readonly string[] options = new string[ChirperPolls.MaxOptions];
    private int durationIndex;

    public ChirperPollDraft()
    {
        Reset();
    }

    public bool Active { get; private set; }

    public int OptionCount { get; private set; }

    public int DurationIndex => durationIndex;

    public int DurationHours => ChirperPolls.DurationHours[durationIndex];

    public bool CanAddOption => OptionCount < ChirperPolls.MaxOptions;

    public bool CanRemoveOption => OptionCount > ChirperPolls.MinOptions;

    public ref string Option(int index) => ref options[index];

    public void Open()
    {
        Reset();
        Active = true;
    }

    public void Reset()
    {
        Active = false;
        OptionCount = ChirperPolls.MinOptions;
        durationIndex = ChirperPolls.DefaultDurationIndex;
        for (var index = 0; index < options.Length; index++)
        {
            options[index] = string.Empty;
        }
    }

    public void AddOption()
    {
        if (!CanAddOption)
        {
            return;
        }

        options[OptionCount] = string.Empty;
        OptionCount++;
    }

    public void RemoveOption(int optionIndex)
    {
        if (!CanRemoveOption || optionIndex < 0 || optionIndex >= OptionCount)
        {
            return;
        }

        for (var index = optionIndex; index < OptionCount - 1; index++)
        {
            options[index] = options[index + 1];
        }

        options[OptionCount - 1] = string.Empty;
        OptionCount--;
    }

    public void SelectDuration(int index)
    {
        durationIndex = Math.Clamp(index, 0, ChirperPolls.DurationHours.Length - 1);
    }

    public PollDraftIssue Validate()
    {
        for (var index = 0; index < OptionCount; index++)
        {
            var option = Clean(options[index]);
            if (option.Length == 0)
            {
                return PollDraftIssue.EmptyOption;
            }

            if (option.Length > ChirperPolls.MaxOptionLength)
            {
                return PollDraftIssue.OptionTooLong;
            }

            for (var earlier = 0; earlier < index; earlier++)
            {
                if (string.Equals(Clean(options[earlier]), option, StringComparison.OrdinalIgnoreCase))
                {
                    return PollDraftIssue.DuplicateOption;
                }
            }
        }

        return PollDraftIssue.None;
    }

    public PostPollInput ToInput()
    {
        var cleaned = new string[OptionCount];
        for (var index = 0; index < OptionCount; index++)
        {
            cleaned[index] = Clean(options[index]);
        }

        return new PostPollInput(cleaned, DurationHours);
    }

    public static string Clean(string raw)
    {
        var hasControl = false;
        for (var index = 0; index < raw.Length; index++)
        {
            if (char.IsControl(raw[index]))
            {
                hasControl = true;
                break;
            }
        }

        if (!hasControl)
        {
            return raw.Trim();
        }

        var buffer = raw.ToCharArray();
        for (var index = 0; index < buffer.Length; index++)
        {
            if (char.IsControl(buffer[index]))
            {
                buffer[index] = ' ';
            }
        }

        return new string(buffer).Trim();
    }
}
