using System;

namespace Nethereum.Siwe.Core.Recap
{
    public class SiweNamespace
    {
        private readonly string _namespace;

        public SiweNamespace(string @namespace)
        {
            Validate(@namespace);

            _namespace = @namespace;
        }

        public override string ToString()
        {
            return _namespace;
        }

        private void Validate(string @namespace)
        {
            var PreviousCharWasAlphanum = false;

            foreach (char c in @namespace)
            {
                if (char.IsLetterOrDigit(c))
                {
                    PreviousCharWasAlphanum = true;
                    continue;
                }

                if ((c == '-') && PreviousCharWasAlphanum)
                {
                    PreviousCharWasAlphanum = false;
                    continue;
                }

                if (c == '-')
                {
                    throw new SiweRecapException("Invalid namespace -> issue with hyphens");
                }

                throw new SiweRecapException("Invalid namespace -> issue with characters");
            }

            if (!PreviousCharWasAlphanum)
            {
                throw new SiweRecapException("Invalid namespace -> issue with hyphens");
            }
        }
    }
}
