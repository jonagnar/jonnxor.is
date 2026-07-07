using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;

namespace Jonnxor.Api.Verification;

/// <summary>
/// Detects whether a raw YAML (or YAML-shaped frontmatter) document contains any comment
/// token, using YamlDotNet's low-level <see cref="Scanner"/> rather than the higher-level
/// deserializer. The scanner tokenizes scalars correctly (a `#` inside a quoted scalar is
/// NOT a comment) while still surfacing genuine comment tokens — including the mid-scalar
/// ` #` shape that silently truncates an unquoted plain scalar
/// (`what: Jol # trailing comment` parses as `what: Jol`, discarding everything after the
/// `#` without raising a parse error). Verified empirically: quoted `#` does not trip this,
/// unquoted mid-scalar and full-line comments both do.
/// </summary>
public static class YamlCommentScanner
{
    public static bool ContainsComment(string yamlText)
    {
        using var reader = new StringReader(yamlText);
        var scanner = new Scanner(reader, skipComments: false);

        while (scanner.MoveNext())
        {
            if (scanner.Current is Comment)
            {
                return true;
            }
        }

        return false;
    }
}
