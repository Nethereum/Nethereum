using System.Linq.Expressions;
using System;

namespace Nethereum.Mud.TableRepository
{
    public interface ITablePredicateBuilder<TTableRecord, TKey, TValue> : ITablePredicateBuilder
        where TTableRecord : TableRecord<TKey, TValue>, new()
        where TValue : class, new() where TKey : class, new()
    {
        //public string PredicateExpression { get; }
        public string Address { get; }
        byte[] TableResourceIdEncoded { get; }

        ITablePredicateBuilder<TTableRecord, TKey, TValue> Equals(Expression<Func<TKey, object>> property, object value);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> NotEquals(Expression<Func<TKey, object>> property, object value);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> AndEqual(Expression<Func<TKey, object>> property, object value);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> OrEqual(Expression<Func<TKey, object>> property, object value);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> AndNotEqual(Expression<Func<TKey, object>> property, object value);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> OrNotEqual(Expression<Func<TKey, object>> property, object value);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> And(ITablePredicateBuilder other);
        ITablePredicateBuilder<TTableRecord, TKey, TValue> Or(ITablePredicateBuilder other);
    }
}