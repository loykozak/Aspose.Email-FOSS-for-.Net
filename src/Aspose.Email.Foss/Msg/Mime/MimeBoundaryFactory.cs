namespace Aspose.Email.Foss.Msg.Mime;

internal sealed class MimeBoundaryFactory
{
    private int _counter;

    public string NextBoundary(string prefix = "AsposeEmailFoss")
    {
        _counter++;
        return $"----={prefix}_{_counter:X8}";
    }
}
