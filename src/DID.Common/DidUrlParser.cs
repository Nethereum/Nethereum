using System;
using System.Text.RegularExpressions;

namespace Did.Common
{

    //Credits to:
    //https://www.w3.org/TR/did-core/#did-url-syntax
    //https://github.com/decentralized-identity/did-resolver/blob/master/src/resolver.ts 
    //https://github.com/decentralized-identity/did-common-java/blob/master/src/main/resources/did.abnf
    //https://abnf.msweet.org/index.php abnf to regex

    public class DidUrlParser
    {
        public static string ID_CHAR = "[a-zA-Z0-9_.-]"; // idchar = ALPHA / DIGIT / "." / "-" / "_" ;
        public static string METHOD_NAME = "(?<MethodName>[a-zA-Z0-9_]+)";  // method-name        = 1*method-char ;
                                                               // method-char        = %x61-7A / DIGIT ;
        public static string METHOD_ID = $"(?<MethodId>{ID_CHAR}+(:{ID_CHAR}+)*)";

        //param-char = ALPHA / DIGIT / "." / "-" / "_" / ":" / Note
        //param              = param-name [ "=" param-value ] ;
        //param-name         = 1*param-char ;
        //param-value        = *param-char ;                                              
        public static string PARAM_CHAR = "[a-zA-Z0-9_.:%-]"; 
        public static string PARAM = $";(?<ParamName>{PARAM_CHAR}+)=(?<ParamValue>{PARAM_CHAR}*)";
        public static string PARAMS = $"(?<Params>({PARAM})*)";

        public static string PATH = @"(?<Path>\/[^#?]*)?";
        public static string QUERY = "(?<Query>[?][^#]*)?";
        public static string FRAGMENT = @"(?<Fragment>\#.*)?";
        
        public static string DID_URL = $"^did:{METHOD_NAME}:{METHOD_ID}{PARAMS}{PATH}{QUERY}{FRAGMENT}$";

        private static Regex Regex = new Regex(DID_URL, RegexOptions.IgnoreCase);

        public static DidUrl Parse(string didUrl)
        {
            if (string.IsNullOrWhiteSpace(didUrl))
                throw new ArgumentException("Did url cannot be null or empty", nameof(didUrl));
            var matches = Regex.Matches(didUrl);

            if(matches.Count > 0)
            {
                var didUrlDecoded = new DidUrl();
                var fullMatch = matches[0];

                var methodName = fullMatch.Groups["MethodName"].Captures[0].Value;
                var methodId = fullMatch.Groups["MethodId"].Captures[0].Value;
                didUrlDecoded.Did = $"did:{methodName}:{methodId}";
                didUrlDecoded.Method = methodName;
                didUrlDecoded.Id = methodId;
                didUrlDecoded.Url = didUrl;

                var paramsMatched = fullMatch.Groups["Params"];
                if(paramsMatched.Length > 0)
                {
                    var paramNames = fullMatch.Groups["ParamName"];
                    var paramValues = fullMatch.Groups["ParamValue"];

                    for(var i = 0; i< paramNames.Captures.Count; i++)
                    {
                        didUrlDecoded.Params[paramNames.Captures[i].Value] = paramValues.Captures[i].Value;
                    }
                }

                var path = fullMatch.Groups["Path"];
                if (path.Captures.Count > 0) didUrlDecoded.Path = path.Captures[0].Value;

                var fragment = fullMatch.Groups["Fragment"];
                if (fragment.Captures.Count > 0) didUrlDecoded.Fragment = fragment.Captures[0].Value.Substring(1);

                var query = fullMatch.Groups["Query"];
                if (query.Captures.Count > 0) didUrlDecoded.Query = query.Captures[0].Value.Substring(1);

                return didUrlDecoded;
            }
            throw new ArgumentException("Invalid Did Url", nameof(didUrl));
        } 
        
    }

}
