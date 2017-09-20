namespace Connection
{
    internal static class Constant
    {
        internal enum PackageTag: byte
        {
            LOGIN,
            REPLY,
            PUSH
        }

        internal const int HEAD_LENGTH = 2;

        internal const int UID_LENGTH = 4;

        internal const int TYPE_LENGTH = 1;
    }
}
