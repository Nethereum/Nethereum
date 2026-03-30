using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Nethereum.DID
{
    public static class DidUrlParser
    {
        private const string PCT_ENCODED = "(?:%[0-9a-fA-F]{2})";
        private const string ID_CHAR = "(?:[a-zA-Z0-9._-]|" + PCT_ENCODED + ")";
        private const string METHOD = "([a-z0-9]+)";
        private const string METHOD_ID = "((?:" + ID_CHAR + "*:)*(" + ID_CHAR + "+))";
        private const string PARAM_CHAR = "[a-zA-Z0-9_.:%-]";
        private const string PARAM = ";(" + PARAM_CHAR + "+)=(" + PARAM_CHAR + "*)";
        private const string PARAMS = "((" + PARAM + ")*)";
        private const string PATH = "(/[^#?]*)?";
        private const string QUERY = "(\\?[^#]*)?";
        private const string FRAGMENT = "(#.*)?";

        private static readonly string DID_URL_PATTERN =
            "^did:" + METHOD + ":" + METHOD_ID + PARAMS + PATH + QUERY + FRAGMENT + "$";

        private static readonly Regex DidUrlRegex = new Regex(DID_URL_PATTERN, RegexOptions.Compiled);
        private static readonly Regex ParamRegex = new Regex(PARAM, RegexOptions.Compiled);

        public static DidUrl Parse(string didUrl)
        {
            if (string.IsNullOrEmpty(didUrl))
                throw new ArgumentException("DID URL cannot be null or empty.", "didUrl");

            var match = DidUrlRegex.Match(didUrl);
            if (!match.Success)
                throw new FormatException("Invalid DID URL format: " + didUrl);

            return BuildDidUrl(match, didUrl);
        }

        public static bool TryParse(string didUrl, out DidUrl result)
        {
            result = null;
            if (string.IsNullOrEmpty(didUrl))
                return false;

            var match = DidUrlRegex.Match(didUrl);
            if (!match.Success)
                return false;

            result = BuildDidUrl(match, didUrl);
            return true;
        }

        private static DidUrl BuildDidUrl(Match match, string originalUrl)
        {
            var method = match.Groups[1].Value;
            var id = match.Groups[2].Value;
            var paramsString = match.Groups[4].Value;
            var path = match.Groups[8].Value;
            var query = match.Groups[9].Value;
            var fragment = match.Groups[10].Value;

            var did = "did:" + method + ":" + id;

            var parsedParams = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(paramsString))
            {
                var paramMatches = ParamRegex.Matches(paramsString);
                for (int i = 0; i < paramMatches.Count; i++)
                {
                    var paramMatch = paramMatches[i];
                    parsedParams[paramMatch.Groups[1].Value] = paramMatch.Groups[2].Value;
                }
            }

            return new DidUrl
            {
                Did = did,
                Url = originalUrl,
                Method = method,
                Id = id,
                Path = string.IsNullOrEmpty(path) ? null : path,
                Fragment = string.IsNullOrEmpty(fragment) ? null : fragment.Substring(1),
                Query = string.IsNullOrEmpty(query) ? null : query.Substring(1),
                Params = parsedParams
            };
        }
    }
}
