using System;

namespace Nethereum.Siwe.Recap
{
    public class SiweNamespace
    {
        private readonly string _namespace;

        public SiweNamespace(string nmspace)
        {
            Validate(nmspace);

            _namespace = nmspace;
        }

        public override string ToString()
        {
            return _namespace;
        }

        private void Validate(string nmspace)
        {
            var PreviousCharWasAlphanum = false;

            foreach (char c in nmspace.ToCharArray())
            {
                if (Char.IsLetterOrDigit(c))
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
