namespace Nethereum.DevP2P.Sync.FullSync
{
    public enum HeaderSweepSource
    {
        Override,
        Cursor,
        Pivot,
        CursorStale,
    }

    public static class HeaderSweepBoundsResolver
    {
        public static (ulong From, ulong To, HeaderSweepSource Source) ResolveSweep(
            (ulong From, ulong To)? overrideBounds, ulong pivot, ulong cursor, bool cursorHeaderExists)
        {
            if (overrideBounds.HasValue)
                return (overrideBounds.Value.From, overrideBounds.Value.To, HeaderSweepSource.Override);

            if (cursor > 0 && cursor < pivot)
                return cursorHeaderExists
                    ? (cursor, 0UL, HeaderSweepSource.Cursor)
                    : (pivot, 0UL, HeaderSweepSource.CursorStale);

            return (pivot, 0UL, HeaderSweepSource.Pivot);
        }
    }
}
