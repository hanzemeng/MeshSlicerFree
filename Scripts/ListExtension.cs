using System.Linq;
using System.Collections.Generic;

namespace Hanzzz.MeshSlicerFree
{

// copied from https://stackoverflow.com/questions/12231569/is-there-in-c-sharp-a-method-for-listt-like-resize-in-c-for-vectort
public static class ListExtension
{
    public static void Resize<T>(this List<T> list, int sz, T c)
    {
        int cur = list.Count;
        if(sz < cur)
            list.RemoveRange(sz, cur - sz);
        else if(sz > cur)
        {
            if(sz > list.Capacity)//this bit is purely an optimisation, to avoid multiple automatic capacity changes.
              list.Capacity = sz;
            list.AddRange(Enumerable.Repeat(c, sz - cur));
        }
    }
    public static void Resize<T>(this List<T> list, int sz) where T : new()
    {
        Resize(list, sz, new T());
    }
}

}
