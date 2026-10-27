using System;
using System.Collections.Generic;
using System.Linq;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Pure compatibility rules for the Four Saints clothing data extension.</summary>
    public static class FourSaintsClothingRules
    {
        /// <summary>Returns non-null items ordered by their display order.</summary>
        /// <typeparam name="T">Reference type stored in the sparse array.</typeparam>
        /// <param name="items">Sparse source sequence.</param>
        /// <param name="displayOrder">Display-order selector, called only for non-null items.</param>
        /// <returns>A stable, deferred ordered sequence without null entries.</returns>
        public static IEnumerable<T> NonNullInDisplayOrder<T>(
            IEnumerable<T> items,
            Func<T, int> displayOrder) where T : class
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (displayOrder == null) throw new ArgumentNullException(nameof(displayOrder));
            return items.Where(item => item != null).OrderBy(displayOrder);
        }
    }
}
