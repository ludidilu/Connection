namespace Connection
{
    internal static class Constant
    {
        internal enum PackageTag: byte
        {
            REPLY,
            PUSH
        }

        internal const int HEAD_LENGTH = 2;

        internal const int TYPE_LENGTH = 1;
    }
}
