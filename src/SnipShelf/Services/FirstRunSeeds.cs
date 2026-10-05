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

                Saving something new
                  Ctrl+N starts a new snippet. Title it, paste the body, add tags — a tag is
                  just a word, and typing one you already use reuses it.

                Finding it again
                  The search box matches titles, bodies, and tag names as you type. Click a tag
                  chip to narrow the list to it. Star the ones you reach for daily and they show
                  up under Favorites.

                Copying
                  Enter on a list item copies it. Ctrl+Shift+C copies whatever you are looking at.

                Snippets and prompts
                  Same thing, different label. "Prompt" is there so your AI prompts stop mixing
                  in with your one-liners; the filter is one click away.

                This note is a normal snippet. Edit it, or delete it — it will not come back.
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
                ^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$

                Matches strict semver 2.0.0: major.minor.patch, optional -prerelease, optional
                +build metadata.

                The leading (0|[1-9]\d*) on each number is the part people get wrong — it rejects
                "01.2.3", which semver does not allow.

                Anchors matter: without ^ and $ this happily matches "1.2.3-nope" inside a longer
                string.
                """,
        },
    ];
}
