﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Lephone.Data;

namespace Lephone.Linq
{
    public class LinqQueryProvider<T, TKey> : IOrderedQueryable<T>, IQueryProvider where T : LinqObjectModel<T, TKey>
    {
        private readonly Expression expression;

        public LinqQueryProvider(Expression expression)
        {
            this.expression = expression;
        }

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            LinqExpressionParser<T> lep = new LinqExpressionParser<T>(this.expression);
            var list = DbEntry.From<T>().Where(lep.condition).OrderBy(lep.orderby).Select();
            return ((IEnumerable<T>)list).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            // never used ???
            return GetEnumerator();
        }

        #endregion

        #region IQueryable Members

        public Type ElementType
        {
            get { return typeof(T); }
        }

        public Expression Expression
        {
            get
            {
                return expression ?? Expression.Constant(this);
            }
        }

        public IQueryProvider Provider
        {
            get { return this; }
        }

        #endregion

        #region IQueryProvider Members

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new LinqQueryProvider<T, TKey>(expression) as IQueryable<TElement>;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new LinqQueryProvider<T, TKey>(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            // never used ???
            return (TResult)CreateQuery(expression).GetEnumerator();
        }

        public object Execute(Expression expression)
        {
            // never used ???
            return CreateQuery(expression).GetEnumerator();
        }

        #endregion
    }
}
