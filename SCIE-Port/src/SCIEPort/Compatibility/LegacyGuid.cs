using System;

namespace SCIEPort.Compatibility
{
    // The user's Android runtime lacks Guid.TryParse. Keep persisted GUID text unchanged.
    internal static class LegacyGuid
    {
        internal static bool TryParse(string text,out Guid value)
        {
            value=Guid.Empty;
            if(text==null)return false;
            try{value=new Guid(text);return true;}
            catch(FormatException){return false;}
            catch(OverflowException){return false;}
            catch(ArgumentException){return false;}
        }
    }
}
