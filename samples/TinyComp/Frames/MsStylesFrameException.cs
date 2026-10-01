namespace TinyComp;

internal sealed class MsStylesFrameException(string message, Exception? inner = null) : Exception(message, inner);
