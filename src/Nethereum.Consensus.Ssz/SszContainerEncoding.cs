using System;

namespace Nethereum.Consensus.Ssz
{
    internal static class SszContainerEncoding
    {
        public static byte[] Combine(byte[] fixedSection, params byte[][] dynamicSections)
        {
            if (fixedSection == null) throw new ArgumentNullException(nameof(fixedSection));
            if (dynamicSections == null) throw new ArgumentNullException(nameof(dynamicSections));

            var totalLength = fixedSection.Length;
            foreach (var section in dynamicSections)
            {
                if (section == null) throw new ArgumentNullException(nameof(dynamicSections), "Dynamic section cannot be null.");
                totalLength += section.Length;
            }

            var result = new byte[totalLength];
            Buffer.BlockCopy(fixedSection, 0, result, 0, fixedSection.Length);

            var offset = fixedSection.Length;
            foreach (var section in dynamicSections)
            {
                Buffer.BlockCopy(section, 0, result, offset, section.Length);
                offset += section.Length;
            }

            return result;
        }
    }
}
