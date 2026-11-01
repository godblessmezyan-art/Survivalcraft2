using System;
using System.Globalization;

namespace SCIEPort.Compatibility
{
    internal static class LegacyParsing
    {
        // World permission masks are invariant decimal text. Avoid UInt32's parsing APIs.
        internal static bool TryUInt32(string text,out uint value)
        {
            value=0;if(text==null)return false;
            int start=0,end=text.Length;
            while(start<end&&IntegerSpace(text[start]))start++;
            while(end>start&&IntegerSpace(text[end-1]))end--;
            if(start==end)return false;
            bool negative=text[start]=='-';if(negative||text[start]=='+')start++;
            if(start==end)return false;
            uint parsed=0;
            for(int i=start;i<end;i++)
            {
                int digit=text[i]-'0';if(digit<0||digit>9||parsed>429496729u||parsed==429496729u&&digit>5)return false;
                parsed=parsed*10+(uint)digit;
            }
            if(negative&&parsed!=0)return false;value=parsed;return true;
        }
        private static bool IntegerSpace(char c)=>c==' '||c=='\t'||c=='\r'||c=='\n'||c=='\f'||c=='\v';
        internal static bool TryBoolean(string text,out bool value)
        {
            value=false;if(text==null)return false;text=text.Trim();
            if(string.Equals(text,"true",StringComparison.OrdinalIgnoreCase)){value=true;return true;}
            return string.Equals(text,"false",StringComparison.OrdinalIgnoreCase);
        }
        internal static bool TryInt32(string text,NumberStyles style,IFormatProvider provider,out int value)
        {
            value=0;if(text==null)return false;
            try{value=int.Parse(text,style,provider);return true;}
            catch(FormatException){return false;}catch(OverflowException){return false;}
        }
        internal static bool TrySingle(string text,NumberStyles style,IFormatProvider provider,out float value)
        {
            value=0;if(text==null)return false;
            try{value=float.Parse(text,style,provider);return true;}
            catch(FormatException){return false;}catch(OverflowException){return false;}
        }
        internal static bool TryEnum<T>(string text,bool ignoreCase,out T value) where T:struct
        {
            value=default(T);if(text==null)return false;
            try{value=(T)Enum.Parse(typeof(T),text,ignoreCase);return true;}
            catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
    }
}
