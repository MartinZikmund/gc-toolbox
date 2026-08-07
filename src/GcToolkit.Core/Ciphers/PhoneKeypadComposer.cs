using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// The pure state machine behind the interactive multitap keypad — type like an old phone. A key press
/// either starts a new letter or, when it repeats the still-<em>pending</em> key, cycles to that key's
/// next letter (A→B→C→A). The "tap fast vs. slow" feel comes entirely from <em>when</em>
/// <see cref="CommitPending"/> is called: the View restarts a short timer on every press and fires
/// <see cref="CommitPending"/> on timeout, so a fast re-tap cycles the pending letter while a slow one
/// lands on a fresh letter. Keeping the timer out of here leaves the whole machine deterministic and
/// unit-testable. Single-character keys (space) commit immediately — no timeout needed.
/// </summary>
public sealed class PhoneKeypadComposer
{
    private static readonly IReadOnlyDictionary<char, string> Keys = new Dictionary<char, string>
    {
        ['1'] = ".,?!",
        ['2'] = "ABC",
        ['3'] = "DEF",
        ['4'] = "GHI",
        ['5'] = "JKL",
        ['6'] = "MNO",
        ['7'] = "PQRS",
        ['8'] = "TUV",
        ['9'] = "WXYZ",
        ['0'] = " ",
    };

    private readonly StringBuilder _committed = new();
    private readonly List<string> _committedGroups = [];
    private char? _pendingKey;
    private int _pendingIndex;

    /// <summary>The text committed so far (pending letter excluded).</summary>
    public string CommittedText => _committed.ToString();

    /// <summary>The letter currently cycling on the last-pressed key, or <see langword="null"/> if none is pending.</summary>
    public char? PendingChar => _pendingKey is char key ? Keys[key][_pendingIndex] : null;

    /// <summary>Committed text plus the pending letter — what the user sees as they type.</summary>
    public string DisplayText => PendingChar is char c ? _committed.ToString() + c : _committed.ToString();

    /// <summary>The multitap press sequence for everything typed so far (e.g. <c>"44 33 555"</c>),
    /// including the in-progress pending key.</summary>
    public string PressCode
    {
        get
        {
            if (_pendingKey is char key)
            {
                return string.Join(' ', _committedGroups.Append(new string(key, _pendingIndex + 1)));
            }

            return string.Join(' ', _committedGroups);
        }
    }

    /// <summary>Registers a key press. Cycles the pending letter when the same key repeats; otherwise
    /// commits any pending letter and begins a new one. Unknown keys are ignored.</summary>
    public void Press(char key)
    {
        if (!Keys.TryGetValue(key, out var chars))
        {
            return;
        }

        // A single-character key (space) has nothing to cycle — commit it outright.
        if (chars.Length == 1)
        {
            CommitPending();
            _committed.Append(chars[0]);
            _committedGroups.Add(new string(key, 1));
            return;
        }

        if (_pendingKey == key)
        {
            _pendingIndex = (_pendingIndex + 1) % chars.Length;
        }
        else
        {
            CommitPending();
            _pendingKey = key;
            _pendingIndex = 0;
        }
    }

    /// <summary>Locks in the pending letter (called by the View's multitap timer, or before a new letter).</summary>
    public void CommitPending()
    {
        if (_pendingKey is not char key)
        {
            return;
        }

        _committed.Append(Keys[key][_pendingIndex]);
        _committedGroups.Add(new string(key, _pendingIndex + 1));
        _pendingKey = null;
        _pendingIndex = 0;
    }

    /// <summary>Cancels the pending letter if one is cycling; otherwise deletes the last committed character.</summary>
    public void Backspace()
    {
        if (_pendingKey is not null)
        {
            _pendingKey = null;
            _pendingIndex = 0;
            return;
        }

        if (_committed.Length > 0)
        {
            _committed.Remove(_committed.Length - 1, 1);
            _committedGroups.RemoveAt(_committedGroups.Count - 1);
        }
    }

    /// <summary>Resets the composer to empty.</summary>
    public void Clear()
    {
        _committed.Clear();
        _committedGroups.Clear();
        _pendingKey = null;
        _pendingIndex = 0;
    }
}
