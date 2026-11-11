using System;
using System.Collections.Generic;

namespace SCIEPort.Features.Blueprints.Core.Blueprints
{
    // SCNET Android has known missing List.Sort overloads. Keep blueprint order deterministic
    // without depending on them; bottom-up merge sort also bounds work for 10000-cell imports.
    internal static class BlueprintPortableSort
    {
        internal static void Sort<T>(List<T> values, Comparison<T> compare)
        {
            int count=values.Count;
            if(count<2)return;
            T[] source=values.ToArray(), target=new T[count];
            for(int width=1;width<count;width=width>count/2?count:width*2)
            {
                for(int start=0;start<count;start+=width*2)
                {
                    int mid=Math.Min(start+width,count),end=Math.Min(start+width*2,count),left=start,right=mid;
                    for(int output=start;output<end;output++)
                        target[output]=left<mid && (right>=end || compare(source[left],source[right])<=0) ? source[left++] : source[right++];
                }
                T[] swap=source;source=target;target=swap;
            }
            for(int i=0;i<count;i++)values[i]=source[i];
        }
    }
}
