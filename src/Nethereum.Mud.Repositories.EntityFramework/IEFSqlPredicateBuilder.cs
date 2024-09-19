using Nethereum.Mud.TableRepository;

namespace Nethereum.Mud.Repositories.EntityFramework
{
    public interface IEFSqlPredicateBuilder
    {
       SqlPredicateResult BuildSql(TablePredicate predicate);
    }
}