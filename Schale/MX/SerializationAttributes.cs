namespace Schale.MX
{
    /// <summary>
    /// Marks a member (or every member of a type) that the official server leaves unassigned rather than sending as an empty array/object.
    /// Newtonsoft's DefaultValueHandling.Ignore does not drop empty collections, so the gateway's contract resolver honours this to reproduce the official key set exactly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class)]
    public sealed class OmitWhenEmptyAttribute : Attribute
    {
    }
}
