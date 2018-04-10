using Eto.Drawing;

namespace Nethereum.Generators.Desktop.Core.Infrastructure.UI
{
    public static class SpacingPaddingDefaults
    {
        public const int Space1 = 4;
        public const int Space2 = Space1 + Space1;
        public const int Space3 = Space2 + Space1;
        public const int Space4 = Space3 + Space1;

        public static readonly Size Spacing1 = new Size(Space1, Space1);
        public static readonly Size Spacing2 = new Size(Space2, Space2);
        public static readonly Size Spacing3 = new Size(Space3, Space3);

        public const int PaddUnit1 = Space2;

        public static readonly Padding Padding1 = new Padding(PaddUnit1);
    }
}
