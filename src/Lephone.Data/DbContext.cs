﻿using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Lephone.Core;
using Lephone.Data.Definition;
using Lephone.Data.QuerySyntax;
using Lephone.Data.Common;
using Lephone.Data.Driver;
using Lephone.Data.SqlEntry;
using Lephone.Data.Builder;
using Lephone.Data.Builder.Clause;
using Lephone.Data.Caching;

namespace Lephone.Data
{
    public class DbContext : DataProvider
    {
        public readonly DbTimeProvider DbTime;

        private Dictionary<string, int> _tableNames;
        // for remoting only
        public DbContext() : this(DataSettings.DefaultContext) { }

        protected internal DbContext(string prefix) : this(EntryConfig.GetDriver(prefix)) { }

        protected internal DbContext(DbDriver driver) : base(driver)
        {
            DbTime = new DbTimeProvider(this);
        }

        public static DbContext GetInstance(string prefix)
        {
            return DbEntry.GetContext(prefix);
        }

        public DateTime GetDatabaseTime()
        {
            string sqlstr = "SELECT " + Dialect.DbNowString;
            DateTime dt = Convert.ToDateTime(ExecuteScalar(sqlstr));
            return dt;
        }

        internal void TryCreateTable(Type dbObjectType)
        {
            if (Driver.AutoCreateTable)
            {
                if (_tableNames == null)
                {
                    InitTableNames();
                }
                Debug.Assert(_tableNames != null);
                ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
                if(oi.CreateTables != null)
                {
                    foreach(var type in oi.CreateTables)
                    {
                        ObjectInfo oi1 = ObjectInfo.GetInstance(type);
                        InnerTryCreateTable(type, oi1);
                    }
                }
                else
                {
                    InnerTryCreateTable(dbObjectType, oi);
                }
            }
        }

        private void InnerTryCreateTable(Type dbObjectType, ObjectInfo oi)
        {
            string name = oi.From.MainTableName;
            if (name != null && !_tableNames.ContainsKey(name.ToLower()))
            {
                CreateTableAndRelations(oi, dbObjectType, mt => !_tableNames.ContainsKey(mt.Name.ToLower()));
            }
        }

        private void CreateTableAndRelations(ObjectInfo oi, Type dbObjectType, CallbackReturnHandler<CrossTable, bool> callback)
        {
            IfUsingTransaction(Dialect.NeedCommitCreateFirst, delegate
            {
                Create(dbObjectType);
                if (!string.IsNullOrEmpty(oi.DeleteToTableName))
                {
                    CreateDeleteToTable(dbObjectType);
                }
                foreach (CrossTable mt in oi.CrossTables.Values)
                {
                    if (callback(mt))
                    {
                        Debug.Assert(dbObjectType.Assembly.FullName != null);
                        CreateCrossTable(dbObjectType, mt.HandleType);
                    }
                }
            });
        }

        protected Stack<List<string>> TransLists = new Stack<List<string>>();

        protected internal override void OnBeginTransaction()
        {
            if (DataSettings.CacheEnabled && !DataSettings.CacheClearWhenError)
            {
                TransLists.Push(new List<string>());
            }
        }

        protected internal override void OnCommittedTransaction()
        {
            if (DataSettings.CacheEnabled && !DataSettings.CacheClearWhenError)
            {
                TransLists.Pop();
            }
        }

        protected internal override void OnTransactionError()
        {
            if (DataSettings.CacheEnabled)
            {
                if (DataSettings.CacheClearWhenError)
                {
                    CacheProvider.Instance.Clear();
                }
                else
                {
                    List<string> keyList = TransLists.Pop();
                    foreach (string key in keyList)
                    {
                        CacheProvider.Instance.Remove(key);
                    }
                }
            }
        }

        protected internal virtual void SetCachedObject(object obj)
        {
            string key = KeyGenerator.Instance[obj];
            CacheProvider.Instance[key] = ObjectInfo.CloneObject(obj);
            if (TransLists.Count > 0)
            {
                TransLists.Peek().Add(key);
            }
        }

        private void IfUsingTransaction(bool isUsing, CallbackVoidHandler callback)
        {
            if (isUsing)
            {
                NewTransaction(callback);
            }
            else
            {
                callback();
            }
        }

        private void InitTableNames()
        {
            _tableNames = new Dictionary<string, int>();
            foreach (string s in GetTableNames())
            {
                _tableNames.Add(s.ToLower(), 1);
            }
        }

        public IWhere<T> From<T>() where T : class, IDbObject
        {
            return new QueryContent<T>(this);
        }

        public long GetResultCount(Type dbObjectType, Condition iwc)
        {
            return GetResultCount(dbObjectType, iwc, false);
        }

        public long GetResultCount(Type dbObjectType, Condition iwc, bool isDistinct)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetResultCountStatement(iwc, isDistinct);
            object ro = ExecuteScalar(sql);
            return Convert.ToInt64(ro);
        }

        internal long GetResultCountAvoidSoftDelete(Type dbObjectType, Condition iwc, bool isDistinct)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql;
            if (oi.Composer is SoftDeleteQueryComposer)
            {
                sql = ((SoftDeleteQueryComposer)oi.Composer).GetResultCountStatementWithoutDeleteCheck(iwc, isDistinct);
            }
            else
            {
                sql = oi.Composer.GetResultCountStatement(iwc, isDistinct);
            }
            object ro = ExecuteScalar(sql);
            return Convert.ToInt64(ro);
        }

        public decimal? GetMax(Type dbObjectType, Condition iwc, string columnName)
        {
            object o = GetMaxObject(dbObjectType, iwc, columnName);
            if (o == null) return null;
            return Convert.ToDecimal(o);
        }

        public DateTime? GetMaxDate(Type dbObjectType, Condition iwc, string columnName)
        {
            object o = GetMaxObject(dbObjectType, iwc, columnName);
            if (o == null) return null;
            return Convert.ToDateTime(o);
        }

        public object GetMaxObject(Type dbObjectType, Condition iwc, string columnName)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetMaxStatement(iwc, columnName);
            object ro = ExecuteScalar(sql);
            if (ro == DBNull.Value)
            {
                return null;
            }
            return ro;
        }

        public decimal? GetMin(Type dbObjectType, Condition iwc, string columnName)
        {
            object o = GetMinObject(dbObjectType, iwc, columnName);
            if (o == null) return null;
            return Convert.ToDecimal(o);
        }

        public DateTime? GetMinDate(Type dbObjectType, Condition iwc, string columnName)
        {
            object o = GetMinObject(dbObjectType, iwc, columnName);
            if (o == null) return null;
            return Convert.ToDateTime(o);
        }

        public object GetMinObject(Type dbObjectType, Condition iwc, string columnName)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetMinStatement(iwc, columnName);
            object ro = ExecuteScalar(sql);
            if (ro == DBNull.Value)
            {
                return null;
            }
            return ro;
        }

        public decimal? GetSum(Type dbObjectType, Condition iwc, string columnName)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetSumStatement(iwc, columnName);
            object ro = ExecuteScalar(sql);
            if (ro == DBNull.Value)
            {
                return null;
            }
            return Convert.ToDecimal(ro);
        }

        public DbObjectList<GroupByObject<T1>> GetGroupBy<T1>(Type dbObjectType, Condition iwc, OrderBy order, string columnName)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetGroupByStatement(iwc, order, columnName);
            var list = new DbObjectList<GroupByObject<T1>>();
            IProcessor ip = GetListProcessor(list, dbObjectType);
            DataLoadDirect(ip, typeof(GroupByObject<T1>), sql, true, false);
            return list;
        }

        public DbObjectList<GroupBySumObject<T1, T2>> GetGroupBySum<T1, T2>(Type dbObjectType, Condition iwc, OrderBy order, string groupbyColumnName, string sumColumnName)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetGroupBySumStatement(iwc, order, groupbyColumnName, sumColumnName);
            var list = new DbObjectList<GroupBySumObject<T1, T2>>();
            IProcessor ip = GetListProcessor(list, dbObjectType);
            DataLoadDirect(ip, typeof(GroupBySumObject<T1, T2>), sql, true, false);
            return list;
        }

        public DbObjectList<T> ExecuteList<T>(string sqlStr) where T : class, IDbObject
        {
            return ExecuteList<T>(new SqlStatement(sqlStr));
        }

        public DbObjectList<T> ExecuteList<T>(string sqlStr, params object[] os) where T : class, IDbObject
        {
            return ExecuteList<T>(GetSqlStatement(sqlStr, os));
        }

        public DbObjectList<T> ExecuteList<T>(SqlStatement sql) where T : class, IDbObject
        {
            var t = typeof(T);
            TryCreateTable(t);
            var ret = new DbObjectList<T>();
            IProcessor ip = GetListProcessor(ret, t);
            DataLoadDirect(ip, t, sql, false, false);
            return ret;
        }

        protected static IProcessor GetListProcessor(IList il, Type t)
        {
            if (DataSettings.CacheAnySelectedItem)
            {
                return new CachedListInserter(il, t);
            }
            if (DataSettings.MaxRecords == 0)
            {
                return new ListInserter(il);
            }
            return new LimitedListInserter(il);
        }

        public void FillCollection(IList list, Type returnType, Type dbObjectType, FromClause from, Condition iwc, OrderBy oc, Range lc, bool isDistinct, bool noLazy = false)
        {
            IProcessor ip = GetListProcessor(list, dbObjectType);
            DataLoad(ip, returnType, dbObjectType, from, iwc, oc, lc, isDistinct, noLazy);
        }

        public void DataLoad(IProcessor ip, Type returnType, Type dbObjectType, FromClause from, Condition iwc, OrderBy oc, Range lc, bool isDistinct, bool noLazy)
        {
            TryCreateTable(dbObjectType);
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetSelectStatement(from, iwc, oc, lc, isDistinct, noLazy, returnType);
            DataLoadDirect(ip, returnType, sql, true, noLazy);
        }

        private void DataLoadDirect(IProcessor ip, Type returnType, SqlStatement sql, bool useIndex, bool noLazy)
        {
            long startIndex = sql.StartIndex;
            long endIndex = sql.EndIndex;
            if (Dialect.SupportsRangeStartIndex && endIndex > 0)
            {
                endIndex = endIndex - startIndex + 1;
                startIndex = 1;
            }
            ExecuteDataReader(sql, returnType, delegate(IDataReader dr)
            {
                int count = 0;
                while (dr.Read())
                {
                    count++;
                    if (count >= startIndex)
                    {
                        object di = ObjectInfo.CreateObject(returnType, dr, useIndex, noLazy);
                        if (!ip.Process(di))
                        {
                            break;
                        }
                    }
                    if (endIndex > 0 && count >= endIndex)
                    {
                        break;
                    }
                }
            });
        }

        public T GetObject<T>(object key) where T : class, IDbObject
        {
            return (T)GetObject(typeof(T), key);
        }

        public T GetObject<T>(Condition c) where T : class, IDbObject
        {
            return (T)GetObject(typeof(T), c, null, null);
        }

        public T GetObject<T>(Condition c, OrderBy ob) where T : class, IDbObject
        {
            return (T)GetObject(typeof(T), c, ob);
        }

        public object GetObject(Type t, object key)
        {
            ObjectInfo oi = ObjectInfo.GetInstance(t);
            if (oi.HasOnePrimaryKey)
            {
                if (DataSettings.CacheEnabled && oi.Cacheable)
                {
                    object co = CacheProvider.Instance[KeyGenerator.Instance.GetKey(t, key)];
                    if (co != null)
                    {
                        object objInCache = ObjectInfo.CloneObject(co);
                        return objInCache;
                    }
                }

                string keyname = oi.KeyFields[0].Name;
                object obj = GetObject(t, CK.K[keyname] == key, null, null);

                if (DataSettings.CacheEnabled && oi.Cacheable)
                {
                    if (obj != null)
                    {
                        SetCachedObject(obj);
                    }
                }
                return obj;
            }
            throw new DataException("To call this function, the table must have one primary key.");
        }

        public object GetObject(Type t, Condition c, OrderBy ob)
        {
            return GetObject(t, c, ob, (ob == null) ? null : new Range(1, 1));
        }

        internal object GetObject(Type t, Condition c, OrderBy ob, Range r)
        {
            IList il = new ArrayList();
            FillCollection(il, t, t, null, c, ob, r, false);
            if (il.Count < 1)
            {
                return null;
            }
            return il[0];
        }

        public void Save(object obj)
        {
            var o = new ModelUpdater();
            o.Save(obj);
        }

        public void Insert(object obj)
        {
            var o = new ModelUpdater();
            o.Insert(obj);
        }

        public void Update(object obj)
        {
            var o = new ModelUpdater();
            o.Update(obj);
        }

        public int Delete(object obj)
        {
            var o = new ModelUpdater();
            return o.Delete(obj);
        }

        public int Delete<T>(Condition iwc) where T : class, IDbObject
        {
            Type t = typeof(T);
            TryCreateTable(t);
            ObjectInfo oi = ObjectInfo.GetInstance(t);
            SqlStatement sql = oi.Composer.GetDeleteStatement(iwc);
            return ExecuteNonQuery(sql);
        }

        public void RebuildTables(Assembly assembly)
        {
            var list = DbEntry.GetAllModels(assembly);
            var ctlist = new List<string>();
            foreach (var type in list)
            {
                var oi = ObjectInfo.GetInstance(type);
                if(!string.IsNullOrEmpty(oi.From.MainTableName))
                {
                    DropTable(type, true);
                    CreateTableAndRelations(
                        oi, type, mt =>
                                  {
                                      if (ctlist.Contains(mt.Name))
                                      {
                                          return false;
                                      }
                                      DropTable(mt.Name, true);
                                      ctlist.Add(mt.Name);
                                      return true;
                                  });
                }
            }
        }

        public void DropTable(Type dbObjectType)
        {
            DropTable(dbObjectType, true);
        }

        public void DropTable(Type dbObjectType, bool catchException)
        {
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            string tn = oi.From.MainTableName;
            DropTable(tn, catchException);
            if (oi.HasSystemKey)
            {
                CommonHelper.CatchAll(() => Dialect.ExecuteDropSequence(this, tn));
            }
            foreach (CrossTable mt in oi.CrossTables.Values)
            {
                DropTable(mt.Name, catchException);
            }
        }

        private void DropTable(string tableName, bool catchException)
        {
            string s = "DROP TABLE " + Dialect.QuoteForTableName(tableName);
            var sql = new SqlStatement(s);
            CommonHelper.IfCatchException(catchException, () => ExecuteNonQuery(sql));
            if (Driver.AutoCreateTable && _tableNames != null)
            {
                _tableNames.Remove(tableName.ToLower());
            }
        }

        public void DropAndCreate(Type dbObjectType)
        {
            DropTable(dbObjectType, true);
            Create(dbObjectType);
        }

        public void Create(Type dbObjectType)
        {
            ObjectInfo oi = ObjectInfo.GetInstance(dbObjectType);
            SqlStatement sql = oi.Composer.GetCreateStatement();
            ExecuteNonQuery(sql);
            oi.Context.Dialect.ExecAddDescriptionSql(oi);
            if (Driver.AutoCreateTable && _tableNames != null)
            {
                _tableNames.Add(oi.From.MainTableName.ToLower(), 1);
            }
        }

        public void CreateDeleteToTable(Type dbObjectType)
        {
            var oi = ObjectInfo.GetInstance(dbObjectType);
            var sb = oi.Composer.GetCreateTableStatementBuilder();
            sb.TableName = oi.DeleteToTableName;
            sb.Columns.Add(new ColumnInfo("DeletedOn", typeof(DateTime), false, false, false, false, 0, 0));
            var sql = sb.ToSqlStatement(oi);
            ExecuteNonQuery(sql);
            if (Driver.AutoCreateTable && _tableNames != null)
            {
                _tableNames.Add(oi.DeleteToTableName.ToLower(), 1);
            }
        }

        public void CreateCrossTable(Type t1, Type t2)
        {
            ObjectInfo oi1 = ObjectInfo.GetInstance(t1);
            ObjectInfo oi2 = ObjectInfo.GetInstance(t2);
            if (!(oi1.CrossTables.ContainsKey(t2) && oi2.CrossTables.ContainsKey(t1)))
            {
                throw new DataException("They are not many to many relation ship classes!");
            }
            if (oi1.KeyFields.Length <= 0 || oi2.KeyFields.Length <= 0)
            {
                throw new DataException("The relation table must have key column!");
            }
            CrossTable mt1 = oi1.CrossTables[t2];
            var cts = new CreateTableStatementBuilder(mt1.Name);
            var ls = new List<string> { mt1.ColumeName1, mt1.ColumeName2 };
            ls.Sort();
            cts.Columns.Add(new ColumnInfo(ls[0], oi1.KeyFields[0].FieldType, false, false, false, false, 0, 0));
            cts.Columns.Add(new ColumnInfo(ls[1], oi2.KeyFields[0].FieldType, false, false, false, false, 0, 0));
            // add index
            cts.Indexes.Add(new DbIndex(null, false, (ASC)mt1.ColumeName1));
            cts.Indexes.Add(new DbIndex(null, false, (ASC)mt1.ColumeName2));
            // execute
            SqlStatement sql = cts.ToSqlStatement(oi1);
            ExecuteNonQuery(sql);
            if (Driver.AutoCreateTable && _tableNames != null)
            {
                _tableNames.Add(mt1.Name.ToLower(), 1);
            }
        }

        #region Linq methods

        public T GetObject<T>(Expression<Func<T, bool>> expr) where T : class, IDbObject
        {
            var wc = Linq.ExpressionParser<T>.Parse(expr);
            return GetObject<T>(wc);
        }

        public int Delete<T>(Expression<Func<T, bool>> expr) where T : class, IDbObject
        {
            var wc = Linq.ExpressionParser<T>.Parse(expr);
            return Delete<T>(wc);
        }

        #endregion
    }
}
