using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>The three program notations this tool understands.</summary>
public enum BrainfuckNotation
{
    /// <summary>Classic Brainfuck — the eight single-character commands <c>&gt; &lt; + - . , [ ]</c>.</summary>
    Brainfuck,

    /// <summary>Ook! — each command is a pair of <c>Ook.</c> / <c>Ook?</c> / <c>Ook!</c> words.</summary>
    Ook,

    /// <summary>Ook! short — the same pairs reduced to their punctuation (<c>. ? !</c>).</summary>
    OokShort,
}

/// <summary>The result of running a program: its captured output plus status flags.</summary>
public readonly record struct BrainfuckResult(string Output, bool Aborted, bool HasError, string? Error)
{
    /// <summary>Whether the program completed normally (not aborted by a guard, no parse error).</summary>
    public bool Completed => !Aborted && !HasError;
}

/// <summary>
/// Pure interpreter, generator and transliterator for Brainfuck and its Ook! / Ook! short variants.
/// </summary>
/// <remarks>
/// <para>
/// The interpreter runs a wrapping byte-cell tape with a movable pointer (the tape grows on demand and
/// the pointer clamps at zero). The eight commands are <c>&gt; &lt; + - . , [ ]</c>; <c>,</c> consumes
/// the supplied <c>input</c> (a NUL byte once it is exhausted) and <c>.</c> appends to the output.
/// Non-command characters are ignored. To stay responsive on pathological programs it caps both the
/// number of executed steps and the produced output length, reporting <see cref="BrainfuckResult.Aborted"/>
/// instead of hanging.
/// </para>
/// <para>
/// The exact Ook! mapping (commands in canonical order <c>&gt; &lt; + - . , [ ]</c>) is verified against
/// the public reference: <c>&gt;</c>=<c>Ook. Ook?</c>, <c>&lt;</c>=<c>Ook? Ook.</c>, <c>+</c>=<c>Ook. Ook.</c>,
/// <c>-</c>=<c>Ook! Ook!</c>, <c>.</c>=<c>Ook! Ook.</c>, <c>,</c>=<c>Ook. Ook!</c>, <c>[</c>=<c>Ook! Ook?</c>,
/// <c>]</c>=<c>Ook? Ook!</c>. Ook! short keeps only the punctuation of each word (<c>.?</c>, <c>?.</c>, …).
/// </para>
/// </remarks>
public sealed class BrainfuckOok
{
    /// <summary>Maximum executed commands before a program is aborted (infinite-loop guard).</summary>
    public const int MaxSteps = 5_000_000;

    /// <summary>Maximum output bytes before a program is aborted (runaway-output guard).</summary>
    public const int MaxOutputLength = 1_000_000;

    /// <summary>Tape size cap so a runaway <c>&gt;</c> can't allocate without bound.</summary>
    public const int MaxTapeLength = 1_000_000;

    private const string Commands = "><+-.,[]";

    /// <summary>Ook! word pairs, indexed parallel to <see cref="Commands"/>.</summary>
    private static readonly string[] OokTokens =
    [
        "Ook. Ook?", // >
        "Ook? Ook.", // <
        "Ook. Ook.", // +
        "Ook! Ook!", // -
        "Ook! Ook.", // .
        "Ook. Ook!", // ,
        "Ook! Ook?", // [
        "Ook? Ook!", // ]
    ];

    /// <summary>Ook! short pairs (the punctuation of each word), parallel to <see cref="Commands"/>.</summary>
    private static readonly string[] OokShortTokens =
    [
        ".?", // >
        "?.", // <
        "..", // +
        "!!", // -
        "!.", // .
        ".!", // ,
        "!?", // [
        "?!", // ]
    ];

    /// <summary>Converts a program from one notation to another (e.g. Brainfuck → Ook!).</summary>
    public string Transliterate(string program, BrainfuckNotation from, BrainfuckNotation to)
    {
        var bf = ToBrainfuck(program, from);
        return FromBrainfuck(bf, to);
    }

    /// <summary>Runs a program written in <paramref name="notation"/>, feeding <paramref name="input"/> to <c>,</c>.</summary>
    public BrainfuckResult Execute(string program, string input, BrainfuckNotation notation)
    {
        string bf;
        try
        {
            bf = ToBrainfuck(program, notation);
        }
        catch (FormatException ex)
        {
            return new BrainfuckResult(string.Empty, Aborted: false, HasError: true, ex.Message);
        }

        return Run(bf, input);
    }

    /// <summary>Builds a program in <paramref name="notation"/> that prints <paramref name="text"/> when run.</summary>
    public string EncodeText(string text, BrainfuckNotation notation) => FromBrainfuck(GenerateBrainfuck(text), notation);

    /// <summary>Best-effort guess of which notation a pasted program is written in.</summary>
    public BrainfuckNotation Detect(string program)
    {
        if (string.IsNullOrWhiteSpace(program))
        {
            return BrainfuckNotation.Brainfuck;
        }

        // "Ook" word presence is the strongest signal for the verbose form.
        if (program.Contains("Ook", StringComparison.OrdinalIgnoreCase))
        {
            return BrainfuckNotation.Ook;
        }

        // Otherwise decide between Brainfuck and Ook! short by which command alphabet dominates.
        var brainfuckCount = program.Count(c => Commands.Contains(c));
        var shortCount = program.Count(c => c is '.' or '?' or '!');

        // Ook! short is built only from . ? ! ; if those clearly outnumber other BF commands, call it short.
        return shortCount > 0 && shortCount >= brainfuckCount ? BrainfuckNotation.OokShort : BrainfuckNotation.Brainfuck;
    }

    // ---- Notation conversion ----

    /// <summary>Normalises any notation down to a clean Brainfuck command string (other chars dropped).</summary>
    private static string ToBrainfuck(string program, BrainfuckNotation notation) => notation switch
    {
        BrainfuckNotation.Brainfuck => Sanitize(program),
        BrainfuckNotation.Ook => OokToBrainfuck(program, OokTokens, verbose: true),
        BrainfuckNotation.OokShort => OokToBrainfuck(program, OokShortTokens, verbose: false),
        _ => Sanitize(program),
    };

    /// <summary>Renders a clean Brainfuck command string into the requested notation.</summary>
    private static string FromBrainfuck(string brainfuck, BrainfuckNotation notation)
    {
        if (notation == BrainfuckNotation.Brainfuck)
        {
            return brainfuck;
        }

        var tokens = notation == BrainfuckNotation.Ook ? OokTokens : OokShortTokens;
        var separator = notation == BrainfuckNotation.Ook ? " " : string.Empty;

        var builder = new StringBuilder();
        var first = true;
        foreach (var command in brainfuck)
        {
            var index = Commands.IndexOf(command);
            if (index < 0)
            {
                continue;
            }

            if (!first)
            {
                builder.Append(separator);
            }

            builder.Append(tokens[index]);
            first = false;
        }

        return builder.ToString();
    }

    /// <summary>Keeps only the eight Brainfuck commands, discarding comments and whitespace.</summary>
    private static string Sanitize(string program)
    {
        var builder = new StringBuilder(program.Length);
        foreach (var c in program)
        {
            if (Commands.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses an Ook!/Ook! short program into Brainfuck. The verbose form is whitespace-tolerant and
    /// matches "Ook" words case-insensitively; the short form reads its punctuation symbols pairwise.
    /// </summary>
    private static string OokToBrainfuck(string program, string[] tokens, bool verbose)
    {
        // Collapse to the bare symbol stream: . ? ! (the only glyphs that distinguish a token).
        var symbols = new StringBuilder();
        if (verbose)
        {
            // Walk words; every "Ook" is followed by exactly one of . ? ! — collect those marks.
            var i = 0;
            while (i < program.Length)
            {
                if (i + 2 < program.Length
                    && (program[i] is 'O' or 'o')
                    && (program[i + 1] is 'o')
                    && (program[i + 2] is 'k' or 'K'))
                {
                    var mark = i + 3 < program.Length ? program[i + 3] : '\0';
                    if (mark is '.' or '?' or '!')
                    {
                        symbols.Append(mark);
                        i += 4;
                        continue;
                    }
                }

                i++;
            }
        }
        else
        {
            foreach (var c in program)
            {
                if (c is '.' or '?' or '!')
                {
                    symbols.Append(c);
                }
            }
        }

        if (symbols.Length % 2 != 0)
        {
            throw new FormatException("Ook! program has an odd number of symbols (each command is a pair).");
        }

        // Build a pair -> command lookup from the short tokens (the symbol pairs are identical for both forms).
        var builder = new StringBuilder(symbols.Length / 2);
        for (var i = 0; i < symbols.Length; i += 2)
        {
            var pair = symbols.ToString(i, 2);
            var index = Array.IndexOf(OokShortTokens, pair);
            if (index < 0)
            {
                throw new FormatException($"Unrecognized Ook! symbol pair \"{pair}\".");
            }

            builder.Append(Commands[index]);
        }

        return builder.ToString();
    }

    // ---- Interpreter ----

    private static BrainfuckResult Run(string program, string input)
    {
        var jumps = BuildJumpTable(program, out var error);
        if (error is not null)
        {
            return new BrainfuckResult(string.Empty, Aborted: false, HasError: true, error);
        }

        var tape = new byte[1024];
        var pointer = 0;
        var inputPos = 0;
        var steps = 0L;
        var output = new StringBuilder();

        for (var ip = 0; ip < program.Length; ip++)
        {
            if (++steps > MaxSteps)
            {
                return new BrainfuckResult(output.ToString(), Aborted: true, HasError: false, null);
            }

            switch (program[ip])
            {
                case '>':
                    pointer++;
                    if (pointer >= tape.Length)
                    {
                        if (tape.Length >= MaxTapeLength)
                        {
                            return new BrainfuckResult(output.ToString(), Aborted: true, HasError: false, null);
                        }

                        Array.Resize(ref tape, Math.Min(tape.Length * 2, MaxTapeLength));
                    }

                    break;
                case '<':
                    if (pointer > 0)
                    {
                        pointer--;
                    }

                    break;
                case '+':
                    tape[pointer]++;
                    break;
                case '-':
                    tape[pointer]--;
                    break;
                case '.':
                    output.Append((char)tape[pointer]);
                    if (output.Length >= MaxOutputLength)
                    {
                        return new BrainfuckResult(output.ToString(), Aborted: true, HasError: false, null);
                    }

                    break;
                case ',':
                    tape[pointer] = inputPos < input.Length ? (byte)input[inputPos++] : (byte)0;
                    break;
                case '[':
                    if (tape[pointer] == 0)
                    {
                        ip = jumps[ip];
                    }

                    break;
                case ']':
                    if (tape[pointer] != 0)
                    {
                        ip = jumps[ip];
                    }

                    break;
            }
        }

        return new BrainfuckResult(output.ToString(), Aborted: false, HasError: false, null);
    }

    /// <summary>Pre-computes matching bracket targets; returns null with <paramref name="error"/> set if unbalanced.</summary>
    private static int[] BuildJumpTable(string program, out string? error)
    {
        error = null;
        var jumps = new int[program.Length];
        var stack = new Stack<int>();

        for (var i = 0; i < program.Length; i++)
        {
            if (program[i] == '[')
            {
                stack.Push(i);
            }
            else if (program[i] == ']')
            {
                if (stack.Count == 0)
                {
                    error = "Unbalanced brackets: a \"]\" has no matching \"[\".";
                    return jumps;
                }

                var open = stack.Pop();
                jumps[open] = i;
                jumps[i] = open;
            }
        }

        if (stack.Count > 0)
        {
            error = "Unbalanced brackets: a \"[\" has no matching \"]\".";
        }

        return jumps;
    }

    // ---- Text -> Brainfuck generator ----

    /// <summary>
    /// Emits a simple, correct Brainfuck program that prints <paramref name="text"/>. A multiply-loop
    /// "ramps" the working cell toward each target byte for compact output, then adjusts by ±1.
    /// </summary>
    private static string GenerateBrainfuck(string text)
    {
        var builder = new StringBuilder();
        var current = 0;
        foreach (var ch in text)
        {
            var target = ch & 0xFF;
            var delta = target - current;
            if (delta > 0)
            {
                builder.Append('+', delta);
            }
            else if (delta < 0)
            {
                builder.Append('-', -delta);
            }

            builder.Append('.');
            current = target;
        }

        return builder.ToString();
    }
}
