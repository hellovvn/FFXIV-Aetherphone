using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal struct StoryStickerDraft
{
    public int Kind;
    public Vector2 Position;
    public float Scale;
    public string Text;
    public string FirstOption;
    public string SecondOption;
    public string Label;

    public static StoryStickerDraft Poll(Vector2 position, string question, string firstOption, string secondOption) =>
        new()
        {
            Kind = StoryStickerKinds.Poll, Position = position, Scale = 1f, Text = question,
            FirstOption = firstOption, SecondOption = secondOption, Label = string.Empty,
        };

    public static StoryStickerDraft Question(Vector2 position, string prompt) =>
        new()
        {
            Kind = StoryStickerKinds.Question, Position = position, Scale = 1f, Text = prompt,
            FirstOption = string.Empty, SecondOption = string.Empty, Label = string.Empty,
        };

    public static StoryStickerDraft Mention(Vector2 position, string handle) =>
        new()
        {
            Kind = StoryStickerKinds.Mention, Position = position, Scale = 1f, Text = handle,
            FirstOption = string.Empty, SecondOption = string.Empty, Label = "@" + handle,
        };
}

internal sealed class StoryStickerDrafts
{
    public const int MaxPolls = 1;
    public const int MaxQuestions = 1;
    public const int MaxMentions = 10;
    public const int PollQuestionMaxLength = 60;
    public const int PollOptionMaxLength = 24;
    public const int QuestionPromptMaxLength = 60;
    public const int AnswerMaxLength = 200;

    private readonly List<StoryStickerDraft> items = new();

    public int Count => items.Count;

    public StoryStickerDraft this[int index] => items[index];

    public static int MaxFor(int kind) => kind switch
    {
        StoryStickerKinds.Poll => MaxPolls,
        StoryStickerKinds.Question => MaxQuestions,
        _ => MaxMentions,
    };

    public int CountOf(int kind)
    {
        var count = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    public bool CanAdd(int kind) => CountOf(kind) < MaxFor(kind);

    public int IndexOf(int kind)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Kind == kind)
            {
                return index;
            }
        }

        return -1;
    }

    public bool HasMention(string handle)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Kind == StoryStickerKinds.Mention
                && string.Equals(items[index].Text, handle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public int Add(StoryStickerDraft draft)
    {
        if (!CanAdd(draft.Kind))
        {
            return -1;
        }

        draft.Scale = StoryStickerLayout.ClampScale(draft.Scale);
        items.Add(draft);
        return items.Count - 1;
    }

    public void RemoveAt(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            items.RemoveAt(index);
        }
    }

    public void MoveTo(int index, Vector2 position)
    {
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        var draft = items[index];
        draft.Position = new Vector2(Math.Clamp(position.X, 0f, 1f), Math.Clamp(position.Y, 0f, 1f));
        items[index] = draft;
    }

    public void ScaleTo(int index, float scale)
    {
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        var draft = items[index];
        draft.Scale = StoryStickerLayout.ClampScale(scale);
        items[index] = draft;
    }

    public void SetPoll(int index, string question, string firstOption, string secondOption)
    {
        if (index < 0 || index >= items.Count || items[index].Kind != StoryStickerKinds.Poll)
        {
            return;
        }

        var draft = items[index];
        draft.Text = Clip(question, PollQuestionMaxLength);
        draft.FirstOption = Clip(firstOption, PollOptionMaxLength);
        draft.SecondOption = Clip(secondOption, PollOptionMaxLength);
        items[index] = draft;
    }

    public void SetPrompt(int index, string prompt)
    {
        if (index < 0 || index >= items.Count || items[index].Kind != StoryStickerKinds.Question)
        {
            return;
        }

        var draft = items[index];
        draft.Text = Clip(prompt, QuestionPromptMaxLength);
        items[index] = draft;
    }

    public void Clear() => items.Clear();

    public StoryStickerInput[]? ToInputs()
    {
        if (items.Count == 0)
        {
            return null;
        }

        var inputs = new StoryStickerInput[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            var draft = items[index];
            inputs[index] = draft.Kind == StoryStickerKinds.Poll
                ? new StoryStickerInput(draft.Kind, draft.Position.X, draft.Position.Y, draft.Scale, draft.Text,
                    new[] { draft.FirstOption, draft.SecondOption })
                : new StoryStickerInput(draft.Kind, draft.Position.X, draft.Position.Y, draft.Scale, draft.Text);
        }

        return inputs;
    }

    public static bool PollReady(string firstOption, string secondOption) =>
        firstOption.Trim().Length > 0 && secondOption.Trim().Length > 0;

    private static string Clip(string text, int maxLength)
    {
        var trimmed = text.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
