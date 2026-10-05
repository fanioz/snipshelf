using SnipShelf.Models;

namespace SnipShelf.Services;

/// <summary>
/// What a brand-new vault contains, so the app teaches itself in about thirty seconds.
/// </summary>
/// <remarks>
/// These are ordinary records with nothing marking them as seeds: the user can edit or delete
/// any of them, and the marker in the database Meta table is what stops them from coming back.
/// </remarks>
public static class FirstRunSeeds
{
    public static IReadOnlyList<Snippet> All { get; } =
    [
        new Snippet
        {
            Title = "Welcome — how to use SnipShelf",
            Kind = SnippetKind.Snippet,
            Tags = [new Tag(0, "welcome")],
            Body = """
                SnipShelf keeps the snippets and prompts you actually reuse, on this machine only.

                Finding things
                  The search box matches titles, bodies, and tag names as you type. Click a tag
                  chip to narrow the list to it, or the ★ Favorites chip to see just the snippets
                  marked as favorites. Sorting switches between updated date, title, and created
                  date.

                Reading them
                  Click a snippet in the list and it opens in the pane alongside for you to read.
                  The pane is read-only, so browsing cannot change anything you have saved.

                Snippets and prompts
                  Same thing, different label. "Prompt" is there so your AI prompts stop mixing
                  in with your one-liners.

                Making it yours
                  The Settings page switches the theme between light, dark, and following your
                  system. The whole vault lives in a folder on this machine and nowhere else.

                This note is a normal snippet, and "welcome" is one of its tags — search for
                that whenever you want to find this note again.
                """,
        },
        new Snippet
        {
            Title = "Explain this code",
            Kind = SnippetKind.Prompt,
            Tags = [new Tag(0, "example"), new Tag(0, "prompt")],
            Body = """
                Explain what this code does, then tell me what it assumes.

                Keep it to the shape of the thing: the inputs, the outputs, and the side effects.
                Skip the line-by-line walkthrough unless I ask for one.

                After the explanation, list anything that would break it — bad input, empty
                collections, a second caller, a slow network — and what the failure would look like.

                Code:
                {{paste here}}
                """,
        },
        new Snippet
        {
            Title = "Regex: semver",
            Kind = SnippetKind.Snippet,
            Tags = [new Tag(0, "example"), new Tag(0, "regex")],
            Body = """
                ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?\z

                Matches strict semver 2.0.0: major.minor.patch, optional -prerelease, optional
                +build metadata.

                The leading (0|[1-9][0-9]*) on each number is the part people get wrong — it rejects
                "01.2.3", which semver does not allow.

                Anchors matter: without ^ and \z this happily matches "1.2.3-nope" inside a longer
                string. The pattern spells [0-9] rather than \d, which would also match non-ASCII
                digits, and \z rather than $, which would tolerate one trailing newline.
                """,
        },
    ];
}
