
#region usings

using System;
using System.Collections;
using org.hanzify.llf.Data.Common;
using org.hanzify.llf.Data.Builder.Clause;

#endregion

namespace org.hanzify.llf.Data.Common
{
    public class PagedSelector<T> : IPagedSelector
    {
        protected WhereCondition iwc;
        protected OrderBy oc;
        internal int _PageSize;
        protected DbContext Entry;

        public PagedSelector(WhereCondition iwc, OrderBy oc, int PageSize, DbContext ds)
        {
            this.iwc = iwc;
            this.oc = oc;
            this._PageSize = PageSize;
            this.Entry = ds;
        }

        int IPagedSelector.PageSize
        {
            get { return _PageSize; }
        }

        public long GetResultCount()
        {
            return Entry.GetResultCount(typeof(T), iwc);
        }

        public virtual IList GetCurrentPage(int PageIndex)
        {
            int StartWith = _PageSize * PageIndex;
            int tn = StartWith + _PageSize;
            IList ret = Entry.From<T>().Where(iwc).OrderBy(oc.OrderItems).Range(StartWith + 1, tn).Select();
            return ret;
        }
    }
}